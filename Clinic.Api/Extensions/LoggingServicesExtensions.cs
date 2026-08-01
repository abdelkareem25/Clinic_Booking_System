using Clinic.Api.Logging;
using Microsoft.AspNetCore.Routing;
using Serilog;
using Serilog.Events;
using System.Diagnostics;
using System.Security.Claims;

namespace Clinic.Api.Extensions
{
    public static class LoggingServicesExtensions
    {
        /// <summary>
        /// Replaces the default console logger with structured logging.
        ///
        /// There was no logging configuration at all: an unhandled exception, a failed login and a
        /// deleted patient record all produced exactly nothing durable. Serilog plugs in behind the
        /// ILogger&lt;T&gt; abstractions already used in the code, so no call site changes.
        ///
        /// Output is JSON in non-development environments so a log aggregator can query it by
        /// UserId, TraceId or StatusCode rather than by regular expression.
        /// </summary>
        public static IHostBuilder AddClinicLogging(this IHostBuilder host)
        {
            return host.UseSerilog((context, services, configuration) =>
            {
                configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext()
                    .Enrich.WithProperty("Application", "Clinic.Api")
                    .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)

                    // Must come after the properties above so it can overwrite RequestPath.
                    .Enrich.With(new RequestPathRedactionEnricher(
                        services.GetRequiredService<IHttpContextAccessor>()));

                if (context.HostingEnvironment.IsDevelopment())
                {
                    configuration.WriteTo.Console(
                        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {NewLine}{Exception}");
                }
                else
                {
                    configuration.WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter());
                }
            });
        }

        /// <summary>
        /// One enriched line per request instead of the three noisy framework lines it replaces.
        ///
        /// RequestPath is redacted to the route template by RequestPathRedactionEnricher - see the
        /// note there about patient names appearing in URLs.
        /// </summary>
        public static IApplicationBuilder UseClinicRequestLogging(this IApplicationBuilder app)
        {
            return app.UseSerilogRequestLogging(options =>
            {
                options.MessageTemplate =
                    "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

                // Health-check style noise would drown the signal; a failed request is worth a
                // warning even when it did not throw.
                options.GetLevel = (httpContext, elapsed, exception) =>
                    exception is not null || httpContext.Response.StatusCode >= 500
                        ? LogEventLevel.Error
                        : httpContext.Response.StatusCode >= 400
                            ? LogEventLevel.Warning
                            : LogEventLevel.Information;

                options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
                {
                    diagnosticContext.Set("UserId",
                        httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous");

                    diagnosticContext.Set("ClientIpAddress",
                        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");

                    diagnosticContext.Set("TraceId",
                        Activity.Current?.Id ?? httpContext.TraceIdentifier);

                    if (httpContext.GetEndpoint() is RouteEndpoint endpoint)
                        diagnosticContext.Set("Endpoint", endpoint.DisplayName);
                };
            });
        }
    }
}
