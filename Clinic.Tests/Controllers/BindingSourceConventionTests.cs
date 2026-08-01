using Clinic.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace Clinic.Tests.Controllers
{
    /// <summary>
    /// Architecture tests for TODO #6 (finding C6).
    ///
    /// APIBaseController carries [ApiController]. Its binding source inference rules treat an
    /// unannotated complex-typed action parameter as [FromBody]. A GET request carries no body and
    /// no Content-Type, so such an action can only ever answer 415 Unsupported Media Type.
    ///
    /// Three of the four list endpoints had [FromQuery]; GetAllAppointments did not. That kind of
    /// inconsistency is exactly what a convention test is for.
    /// </summary>
    public sealed class BindingSourceConventionTests
    {
        private static IEnumerable<Type> ControllerTypes() =>
            typeof(APIBaseController).Assembly
                .GetTypes()
                .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && t is { IsAbstract: false })
                .OrderBy(t => t.Name);

        private static IEnumerable<MethodInfo> ActionsWith<TAttribute>() where TAttribute : Attribute =>
            ControllerTypes()
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                .Where(m => m.GetCustomAttributes<TAttribute>().Any());

        [Fact]
        public void Get_Actions_Never_Leave_A_Complex_Parameter_To_Be_Inferred_From_The_Body()
        {
            var offenders = ActionsWith<HttpGetAttribute>()
                .SelectMany(method => method.GetParameters().Select(parameter => (method, parameter)))
                .Where(x => IsInferredAsBody(x.parameter))
                .Select(x => $"{x.method.DeclaringType!.Name}.{x.method.Name} " +
                             $"({x.parameter.ParameterType.Name} {x.parameter.Name}) " +
                             "-> add [FromQuery]; otherwise [ApiController] infers [FromBody] and the endpoint returns 415.")
                .ToList();

            Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
        }

        [Fact]
        public void Delete_Actions_Never_Leave_A_Complex_Parameter_To_Be_Inferred_From_The_Body()
        {
            // DELETE has the same problem for the same reason. No action has one today; this keeps
            // it that way.
            var offenders = ActionsWith<HttpDeleteAttribute>()
                .SelectMany(method => method.GetParameters().Select(parameter => (method, parameter)))
                .Where(x => IsInferredAsBody(x.parameter))
                .Select(x => $"{x.method.DeclaringType!.Name}.{x.method.Name} ({x.parameter.Name})")
                .ToList();

            Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
        }

        [Fact]
        public void GetAllAppointments_Binds_Its_Specification_Parameters_From_The_Query_String()
        {
            // The specific regression.
            var action = typeof(AppointmentsController)
                .GetMethod(nameof(AppointmentsController.GetAllAppointments))!;

            var parameter = Assert.Single(action.GetParameters());

            Assert.NotNull(parameter.GetCustomAttribute<FromQueryAttribute>());
        }

        [Fact]
        public void Every_Paged_List_Endpoint_Binds_From_The_Query_String()
        {
            // All four list endpoints should agree. Three did; one did not.
            var listActions = new (Type Controller, string Action)[]
            {
                (typeof(AppointmentsController), nameof(AppointmentsController.GetAllAppointments)),
                (typeof(DoctorsController),      nameof(DoctorsController.GetAll)),
                (typeof(PatientsController),     nameof(PatientsController.GetAll)),
                (typeof(ScheduleController),     nameof(ScheduleController.GetSchedules))
            };

            foreach (var (controller, action) in listActions)
            {
                var parameter = controller.GetMethod(action)!.GetParameters()
                    .Single(p => p.ParameterType.Name.EndsWith("SpecParams", StringComparison.Ordinal));

                Assert.True(parameter.GetCustomAttribute<FromQueryAttribute>() is not null,
                    $"{controller.Name}.{action} is missing [FromQuery] on {parameter.Name}.");
            }
        }

        [Fact]
        public void The_Convention_Guard_Actually_Inspects_Some_Actions()
        {
            // Protects against the guards above passing because discovery found nothing.
            Assert.True(ControllerTypes().Count() >= 5);
            Assert.True(ActionsWith<HttpGetAttribute>().Count() >= 8);
        }

        /// <summary>
        /// Mirrors [ApiController]'s inference: a complex type with no explicit binding source
        /// attribute is bound from the request body.
        /// </summary>
        private static bool IsInferredAsBody(ParameterInfo parameter)
        {
            if (HasExplicitBindingSource(parameter)) return false;
            return IsComplexType(parameter.ParameterType);
        }

        private static bool HasExplicitBindingSource(ParameterInfo parameter) =>
            parameter.GetCustomAttributes()
                     .Any(a => a is FromQueryAttribute or FromRouteAttribute or FromBodyAttribute
                                 or FromFormAttribute or FromHeaderAttribute or FromServicesAttribute);

        private static bool IsComplexType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;

            if (type == typeof(CancellationToken)) return false;   // MVC binds this specially
            if (type.IsPrimitive || type.IsEnum) return false;
            if (type == typeof(string) || type == typeof(decimal) || type == typeof(Guid) ||
                type == typeof(DateTime) || type == typeof(DateTimeOffset) ||
                type == typeof(TimeSpan) || type == typeof(DateOnly) || type == typeof(TimeOnly))
                return false;

            return true;
        }
    }
}
