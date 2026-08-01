using Clinic.Api.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace Clinic.Tests.Controllers
{
    /// <summary>
    /// Architecture tests for TODO #8 (finding C10).
    ///
    /// The template "patient{patientName}" is legal route syntax, so nothing complained - it simply
    /// matched /api/appointments/patientSara instead of /api/appointments/patient/Sara. A typo of a
    /// single missing character silently produced a different, undiscoverable endpoint.
    ///
    /// The rule enforced here: a route segment is either entirely literal or entirely a parameter.
    /// </summary>
    public sealed class RouteTemplateConventionTests
    {
        private sealed record Route(string Controller, string Action, string Template);

        private static IEnumerable<Route> AllRoutes() =>
            typeof(APIBaseController).Assembly
                .GetTypes()
                .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>()
                        .Where(a => !string.IsNullOrWhiteSpace(a.Template))
                        .Select(a => new Route(t.Name, m.Name, a.Template!))))
                .OrderBy(r => r.Controller).ThenBy(r => r.Action);

        [Fact]
        public void No_Route_Segment_Mixes_Literal_Text_With_A_Parameter()
        {
            var offenders = new List<string>();

            foreach (var route in AllRoutes())
            {
                foreach (var segment in route.Template.Split('/', StringSplitOptions.RemoveEmptyEntries))
                {
                    var isParameter = segment.StartsWith('{') && segment.EndsWith('}');
                    var containsParameter = segment.Contains('{');

                    if (containsParameter && !isParameter)
                    {
                        offenders.Add(
                            $"{route.Controller}.{route.Action}: template '{route.Template}' has segment " +
                            $"'{segment}' mixing a literal with a parameter. A '/' separator is almost " +
                            "certainly missing.");
                    }
                }
            }

            Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
        }

        [Fact]
        public void GetByPatientName_Is_Reachable_At_A_Separated_Route()
        {
            var template = typeof(AppointmentsController)
                .GetMethod(nameof(AppointmentsController.GetByPatientName))!
                .GetCustomAttribute<HttpGetAttribute>()!.Template;

            Assert.Equal("patient/{patientName}", template);
        }

        [Fact]
        public void The_Two_Lookup_By_Name_Endpoints_Follow_The_Same_Shape()
        {
            // GetByDoctorName was already correct; the pair should be symmetric.
            var doctorTemplate = typeof(AppointmentsController)
                .GetMethod(nameof(AppointmentsController.GetByDoctorName))!
                .GetCustomAttribute<HttpGetAttribute>()!.Template;

            var patientTemplate = typeof(AppointmentsController)
                .GetMethod(nameof(AppointmentsController.GetByPatientName))!
                .GetCustomAttribute<HttpGetAttribute>()!.Template;

            Assert.Equal("doctor/{doctorName}", doctorTemplate);
            Assert.Equal("patient/{patientName}", patientTemplate);
        }

        [Fact]
        public void The_Route_Guard_Actually_Inspects_Some_Templates()
        {
            // Protects against the guard passing because discovery found nothing.
            Assert.True(AllRoutes().Count() >= 10,
                $"Expected at least 10 templated routes, found {AllRoutes().Count()}.");
        }
    }
}
