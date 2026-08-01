using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace Clinic.Api.Middleware
{
    /// <summary>
    /// Turns any unhandled exception into a single, predictable error contract.
    ///
    /// There was no exception handling in the pipeline at all. In Production that meant a bare 500
    /// with an empty body and nothing written anywhere - during this review several regression runs
    /// produced exactly that, and the only clue was "The input does not contain any JSON tokens".
    /// In Development the automatic developer exception page leaked source, stack frames and
    /// connection strings to the caller instead.
    ///
    /// Every failure now answers with RFC 7807 application/problem+json carrying a traceId, so a
    /// client can report an error and it can be found in the logs.
    /// </summary>
    public sealed class GlobalExceptionHandler : IExceptionHandler
    {
        /// <summary>Nginx's convention for "client closed the request". Not an IANA status code.</summary>
        private const int ClientClosedRequest = 499;

        private readonly ILogger<GlobalExceptionHandler> _logger;
        private readonly IHostEnvironment _environment;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment)
        {
            _logger = logger;
            _environment = environment;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            // Headers are already on the wire; anything written now would corrupt the response.
            // Returning false lets the server tear the connection down instead.
            if (httpContext.Response.HasStarted)
            {
                _logger.LogError(exception,
                    "Unhandled exception after the response had started. {Method} {Path}",
                    httpContext.Request.Method, httpContext.Request.Path);
                return false;
            }

            var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

            // A caller hanging up is not a server fault and must not be logged as one, or a user
            // hitting refresh will fill the error log. Relevant once TODO #28 threads cancellation
            // tokens through the request path.
            if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
            {
                _logger.LogInformation("Request aborted by the client. {Method} {Path} TraceId={TraceId}",
                    httpContext.Request.Method, httpContext.Request.Path, traceId);

                httpContext.Response.StatusCode = ClientClosedRequest;
                return true;
            }

            var (status, title) = Map(exception);

            if (status >= StatusCodes.Status500InternalServerError)
            {
                _logger.LogError(exception,
                    "Unhandled exception. {Method} {Path} TraceId={TraceId}",
                    httpContext.Request.Method, httpContext.Request.Path, traceId);
            }
            else
            {
                _logger.LogWarning(exception,
                    "Request failed with {StatusCode}. {Method} {Path} TraceId={TraceId}",
                    status, httpContext.Request.Method, httpContext.Request.Path, traceId);
            }

            var problem = new ProblemDetails
            {
                Status = status,
                Title = title,
                Type = $"https://httpstatuses.io/{status}",
                Instance = httpContext.Request.Path,

                // Internals go to the client ONLY in development. Outside it, the traceId is the
                // link between what the caller saw and what the logs recorded.
                Detail = _environment.IsDevelopment() ? exception.ToString() : null
            };

            problem.Extensions["traceId"] = traceId;

            httpContext.Response.StatusCode = status;
            await httpContext.Response.WriteAsJsonAsync(
                problem, options: null, contentType: "application/problem+json", cancellationToken);

            return true;
        }

        /// <summary>
        /// Maps the exception types this application can currently produce. The richer domain
        /// exceptions (NotFound / Validation / Conflict) arrive with the Application layer in
        /// TODO #32; this switch is where they will be added.
        /// </summary>
        private static (int Status, string Title) Map(Exception exception) => exception switch
        {
            // Optimistic concurrency: someone else changed the row first. Becomes reachable once
            // TODO #21 adds a RowVersion column.
            DbUpdateConcurrencyException =>
                (StatusCodes.Status409Conflict, "The record was modified by another user. Reload and try again."),

            // A unique index or foreign key refused the write.
            DbUpdateException =>
                (StatusCodes.Status409Conflict, "The request conflicts with the current state of the data."),

            UnauthorizedAccessException =>
                (StatusCodes.Status403Forbidden, "You are not allowed to perform this action."),

            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
        };
    }
}
