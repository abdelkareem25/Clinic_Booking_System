using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using System.Diagnostics;
using System.Reflection;
using System.Security.Claims;

namespace Clinic.Api.Logging
{
    /// <summary>
    /// Writes one audit record per access to protected health information.
    ///
    /// The record contains IDENTIFIERS ONLY - who, which operation, which record, what outcome. It
    /// deliberately never contains patient names, phone numbers, dates of birth or request bodies:
    /// an audit log that reproduces the data it is auditing just creates a second, less protected
    /// copy of it, usually somewhere with far looser access control than the database.
    /// </summary>
    public sealed class PhiAccessAuditFilter : IAsyncActionFilter
    {
        private readonly ILogger<PhiAccessAuditFilter> _logger;

        public PhiAccessAuditFilter(ILogger<PhiAccessAuditFilter> logger)
        {
            _logger = logger;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var audit = ResolveAttribute(context);

            if (audit is null)
            {
                await next();
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            var executed = await next();
            stopwatch.Stop();

            var httpContext = context.HttpContext;

            _logger.LogInformation(
                "PHI access: user {UserId} performed {Operation} on {ResourceType}/{ResourceId} " +
                "-> {StatusCode} in {ElapsedMilliseconds}ms from {ClientIpAddress} (trace {TraceId})",
                httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous",
                $"{httpContext.Request.Method} {ActionName(context)}",
                audit.ResourceType,
                ResourceId(context),
                StatusCode(executed),
                stopwatch.ElapsedMilliseconds,
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                Activity.Current?.Id ?? httpContext.TraceIdentifier);

            if (executed.Exception is not null && !executed.ExceptionHandled)
            {
                // The access still happened and still has to be recorded, even though the request
                // did not complete. The exception itself is logged by GlobalExceptionHandler.
                _logger.LogWarning(
                    "PHI access by user {UserId} on {ResourceType}/{ResourceId} failed with {ExceptionType}.",
                    httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous",
                    audit.ResourceType,
                    ResourceId(context),
                    executed.Exception.GetType().Name);
            }
        }

        /// <summary>
        /// The outcome the caller will actually see.
        ///
        /// Reading HttpContext.Response.StatusCode here reports 200 for everything: an action filter
        /// runs after the action returns but BEFORE its IActionResult is executed, so the response
        /// has not been written yet. An audit trail that records the wrong outcome is worse than
        /// none, because it looks authoritative.
        /// </summary>
        private static int StatusCode(ActionExecutedContext executed) =>
            (executed.Result as IStatusCodeActionResult)?.StatusCode
            ?? executed.HttpContext.Response.StatusCode;

        /// <summary>Action-level attribute wins over the controller-level one.</summary>
        private static AuditPhiAccessAttribute? ResolveAttribute(ActionExecutingContext context)
        {
            if (context.ActionDescriptor is not ControllerActionDescriptor descriptor) return null;

            return descriptor.MethodInfo.GetCustomAttribute<AuditPhiAccessAttribute>()
                ?? descriptor.ControllerTypeInfo.GetCustomAttribute<AuditPhiAccessAttribute>();
        }

        private static string ActionName(ActionExecutingContext context) =>
            context.ActionDescriptor is ControllerActionDescriptor descriptor
                ? $"{descriptor.ControllerName}.{descriptor.ActionName}"
                : context.ActionDescriptor.DisplayName ?? "unknown";

        /// <summary>
        /// The record identifier from the route, or "collection" for a list endpoint.
        ///
        /// Only route values that are plausibly identifiers are used. A route like
        /// /api/Appointments/patient/{patientName} carries a patient NAME, which is itself PHI and
        /// must not be written to the audit log.
        /// </summary>
        private static string ResourceId(ActionExecutingContext context)
        {
            if (context.RouteData.Values.TryGetValue("id", out var id) && id is not null)
                return id.ToString()!;

            return "collection";
        }
    }
}
