using Clinic.Api.Controllers;
using Clinic.Api.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Moq;
using Serilog.Events;
using Serilog.Parsing;
using System.Reflection;

namespace Clinic.Tests.Logging
{
    /// <summary>
    /// Tests for TODO #16 (finding H15), covering log redaction.
    ///
    /// UseSerilogRequestLogging records RequestPath verbatim, and this API exposes
    /// /api/Appointments/patient/{patientName}. Without redaction, a single request for
    /// "Sara Ahmed" writes that patient's name into every log sink.
    /// </summary>
    public sealed class RequestPathRedactionTests
    {
        private static LogEvent EventWithPath(string path) => new(
            DateTimeOffset.UtcNow, LogEventLevel.Information, exception: null,
            new MessageTemplate("HTTP {RequestPath}", []),
            [new LogEventProperty("RequestPath", new ScalarValue(path))]);

        private static RequestPathRedactionEnricher EnricherFor(HttpContext? httpContext)
        {
            var accessor = new Mock<IHttpContextAccessor>();
            accessor.SetupGet(a => a.HttpContext).Returns(httpContext);
            return new RequestPathRedactionEnricher(accessor.Object);
        }

        private static HttpContext ContextWithEndpoint(string routeTemplate, string path)
        {
            var context = new DefaultHttpContext();
            context.Request.Path = path;
            context.SetEndpoint(new RouteEndpoint(
                _ => Task.CompletedTask,
                RoutePatternFactory.Parse(routeTemplate),
                order: 0,
                new EndpointMetadataCollection(),
                displayName: routeTemplate));
            return context;
        }

        private static string PathOf(LogEvent logEvent) =>
            ((ScalarValue)logEvent.Properties["RequestPath"]).Value!.ToString()!;

        private static void Enrich(RequestPathRedactionEnricher enricher, LogEvent logEvent) =>
            enricher.Enrich(logEvent, new PropertyFactory());

        [Fact]
        public void A_Patient_Name_In_The_Url_Is_Replaced_By_The_Route_Template()
        {
            // The headline case.
            var logEvent = EventWithPath("/api/Appointments/patient/Sara%20Ahmed");
            var enricher = EnricherFor(ContextWithEndpoint(
                "api/Appointments/patient/{patientName}", "/api/Appointments/patient/Sara Ahmed"));

            Enrich(enricher, logEvent);

            Assert.Equal("/api/Appointments/patient/{patientName}", PathOf(logEvent));
            Assert.DoesNotContain("Sara", PathOf(logEvent), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void An_Identifier_Route_Is_Also_Templated()
        {
            // Record ids are not secret, but templating them keeps log aggregation useful: one
            // series for the endpoint rather than one per patient.
            var logEvent = EventWithPath("/api/Patients/7");
            var enricher = EnricherFor(ContextWithEndpoint("api/Patients/{id}", "/api/Patients/7"));

            Enrich(enricher, logEvent);

            Assert.Equal("/api/Patients/{id}", PathOf(logEvent));
        }

        [Fact]
        public void An_Unmatched_Path_Is_Truncated_Rather_Than_Logged_Whole()
        {
            // A mistyped URL has no template to fall back on but can still carry a name.
            var context = new DefaultHttpContext();
            context.Request.Path = "/api/Appointment/patient/Sara Ahmed";

            var logEvent = EventWithPath("/api/Appointment/patient/Sara%20Ahmed");

            Enrich(EnricherFor(context), logEvent);

            Assert.Equal("/api/Appointment/...", PathOf(logEvent));
            Assert.DoesNotContain("Sara", PathOf(logEvent), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void A_Short_Unmatched_Path_Survives_Intact()
        {
            var context = new DefaultHttpContext();
            context.Request.Path = "/api/Nonsense";

            var logEvent = EventWithPath("/api/Nonsense");

            Enrich(EnricherFor(context), logEvent);

            Assert.Equal("/api/Nonsense", PathOf(logEvent));
        }

        [Fact]
        public void Events_Without_A_Request_Path_Are_Left_Alone()
        {
            // Startup and background log events have no path; the enricher must not invent one.
            var logEvent = new LogEvent(
                DateTimeOffset.UtcNow, LogEventLevel.Information, null,
                new MessageTemplate("Seeded roles", []), []);

            Enrich(EnricherFor(null), logEvent);

            Assert.DoesNotContain("RequestPath", logEvent.Properties.Keys);
        }

        [Fact]
        public void Every_Route_Carrying_A_Name_Is_Covered_By_A_Template()
        {
            // Documents which routes make redaction necessary. TODO #43 replaces them with
            // identifier-based routes; until then this is the exposure being contained.
            var nameRoutes = typeof(AppointmentsController)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .SelectMany(m => m.GetCustomAttributes<Microsoft.AspNetCore.Mvc.HttpGetAttribute>())
                .Select(a => a.Template)
                .Where(t => t is not null && t.Contains("Name}", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Assert.NotEmpty(nameRoutes);
            Assert.Contains(nameRoutes, t => t!.Contains("patientName", StringComparison.Ordinal));
        }

        private sealed class PropertyFactory : Serilog.Core.ILogEventPropertyFactory
        {
            public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false)
                => new(name, new ScalarValue(value));
        }
    }
}
