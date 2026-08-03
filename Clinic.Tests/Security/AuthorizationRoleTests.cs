using Clinic.Api.Controllers;
using Clinic.Domain.Entites.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using System.Reflection;

namespace Clinic.Tests.Security
{
    /// <summary>
    /// Architecture tests for TODO #11 (finding C8).
    ///
    /// A role named in [Authorize(Roles = ...)] that is never seeded produces no error anywhere - the
    /// check simply never passes and the endpoint returns 403 forever. This guard makes the two
    /// halves prove they agree.
    /// </summary>
    public sealed class AuthorizationRoleTests
    {
        private static IEnumerable<Type> ControllerTypes() =>
            typeof(APIBaseController).Assembly
                .GetTypes()
                .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

        private sealed record RoleReference(string Location, string Role);

        private static IEnumerable<RoleReference> ReferencedRoles()
        {
            foreach (var controller in ControllerTypes())
            {
                foreach (var attribute in controller.GetCustomAttributes<AuthorizeAttribute>())
                    foreach (var role in Split(attribute.Roles))
                        yield return new RoleReference(controller.Name, role);

                foreach (var action in controller.GetMethods(
                             BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                         .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any()))
                {
                    foreach (var attribute in action.GetCustomAttributes<AuthorizeAttribute>())
                        foreach (var role in Split(attribute.Roles))
                            yield return new RoleReference($"{controller.Name}.{action.Name}", role);
                }
            }
        }

        private static IEnumerable<string> Split(string? roles) =>
            string.IsNullOrWhiteSpace(roles)
                ? []
                : roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        [Fact]
        public void Every_Role_Named_In_An_Authorize_Attribute_Is_One_The_Seeder_Creates()
        {
            var offenders = ReferencedRoles()
                .Where(r => !ClinicRoles.All.Contains(r.Role, StringComparer.Ordinal))
                .Select(r => $"{r.Location} requires role '{r.Role}', which ClinicIdentityDbContextSeed " +
                             "never creates. That endpoint can only ever return 403.")
                .Distinct()
                .ToList();

            Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
        }

        [Fact]
        public void The_Appointment_Mutation_Endpoints_Require_Clinical_Staff()
        {
            // These two were the only role-protected actions in the codebase, and they were
            // unusable because neither role existed.
            foreach (var name in new[] { nameof(AppointmentsController.Delete), nameof(AppointmentsController.Update) })
            {
                var attribute = typeof(AppointmentsController).GetMethod(name)!
                    .GetCustomAttribute<AuthorizeAttribute>();

                Assert.NotNull(attribute);
                var roles = Split(attribute!.Roles).ToList();

                Assert.Contains(ClinicRoles.Admin, roles);
                Assert.Contains(ClinicRoles.Doctor, roles);
            }
        }

        [Fact]
        public void Role_Names_Are_Unique_And_Non_Empty()
        {
            Assert.Equal(ClinicRoles.All.Length, ClinicRoles.All.Distinct(StringComparer.Ordinal).Count());
            Assert.All(ClinicRoles.All, r => Assert.False(string.IsNullOrWhiteSpace(r)));
        }

        [Fact]
        public void The_Role_Guard_Actually_Inspects_Some_Attributes()
        {
            Assert.True(ReferencedRoles().Any(),
                "No [Authorize(Roles = ...)] attributes were found; the guard would be vacuous.");
        }
    }
}
