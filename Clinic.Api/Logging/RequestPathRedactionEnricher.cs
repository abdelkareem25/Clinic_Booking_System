using Microsoft.AspNetCore.Routing;
using Serilog.Core;
using Serilog.Events;

namespace Clinic.Api.Logging
{
    /// <summary>
    /// Replaces the concrete request path in log events with the route template.
    ///
    /// This is not cosmetic. UseSerilogRequestLogging records RequestPath verbatim, and this API has
    /// routes such as /api/Appointments/patient/{patientName}. A request for
    /// "/api/Appointments/patient/Sara%20Ahmed" would put a patient's name into every log sink,
    /// which is protected health information sitting outside the database and usually under much
    /// weaker access control. Logging "/api/Appointments/patient/{patientName}" instead keeps the
    /// operational value - which endpoint, how often, how slow - with none of the exposure.
    ///
    /// (TODO #43 replaces those name-based routes with identifier-based ones. This still earns its
    /// place afterwards: it stops the next name-bearing route from silently leaking.)
    /// </summary>
    public sealed class RequestPathRedactionEnricher : ILogEventEnricher
    {
        private const string RequestPathProperty = "RequestPath";

        private readonly IHttpContextAccessor _httpContextAccessor;

        public RequestPathRedactionEnricher(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            if (!logEvent.Properties.ContainsKey(RequestPathProperty)) return;

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext is null) return;

            var safePath = httpContext.GetEndpoint() is RouteEndpoint endpoint
                ? "/" + endpoint.RoutePattern.RawText?.TrimStart('/')
                : TruncateUnmatched(httpContext.Request.Path);

            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(RequestPathProperty, safePath));
        }

        /// <summary>
        /// An unmatched request has no template to fall back on, and a mistyped URL can still carry
        /// a name (/api/Appointment/patient/Sara). Keep enough to diagnose the routing problem and
        /// drop the rest.
        /// </summary>
        private static string TruncateUnmatched(PathString path)
        {
            var segments = (path.Value ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries);

            return segments.Length <= 2
                ? "/" + string.Join('/', segments)
                : "/" + string.Join('/', segments.Take(2)) + "/...";
        }
    }
}
