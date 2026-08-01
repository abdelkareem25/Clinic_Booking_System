using Clinic.Api.Controllers;
using Clinic.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace Clinic.Tests.Security
{
    /// <summary>
    /// Architecture tests for TODO #10 (finding C7).
    ///
    /// The API used to protect exactly two actions. Everything else - including a paginated dump of
    /// every patient's name, phone number, date of birth and gender, and anonymous create, update
    /// and delete over those same records - was reachable by anyone with the URL.
    ///
    /// The guard below is an allow-list: any endpoint that becomes anonymous has to be added here
    /// deliberately, in a diff a reviewer will see.
    /// </summary>
    public sealed class AuthorizationCoverageTests
    {
        /// <summary>
        /// The complete set of endpoints permitted to run without authentication.
        ///
        /// Register was on this list until TODO #14 - it is now [Authorize(Roles = Admin)], so Login
        /// is the only way in and the only thing that can be anonymous.
        /// </summary>
        private static readonly HashSet<string> ApprovedAnonymousEndpoints =
        [
            $"{nameof(AccountsController)}.{nameof(AccountsController.Login)}"
        ];

        private static IEnumerable<Type> ControllerTypes() =>
            typeof(APIBaseController).Assembly
                .GetTypes()
                .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract && t != typeof(APIBaseController))
                .OrderBy(t => t.Name);

        private static IEnumerable<MethodInfo> ActionsOf(Type controller) =>
            controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                      .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any());

        private static bool IsAnonymous(MethodInfo action) =>
            action.GetCustomAttribute<AllowAnonymousAttribute>() is not null ||
            action.DeclaringType!.GetCustomAttribute<AllowAnonymousAttribute>() is not null;

        [Fact]
        public void Only_The_Approved_Endpoints_Are_Anonymous()
        {
            var anonymous = ControllerTypes()
                .SelectMany(ActionsOf)
                .Where(IsAnonymous)
                .Select(a => $"{a.DeclaringType!.Name}.{a.Name}")
                .ToHashSet();

            var unexpected = anonymous.Except(ApprovedAnonymousEndpoints).ToList();
            var missing = ApprovedAnonymousEndpoints.Except(anonymous).ToList();

            Assert.True(unexpected.Count == 0,
                "These endpoints are anonymous but not on the approved list: " + string.Join(", ", unexpected));

            Assert.True(missing.Count == 0,
                "These endpoints are on the approved anonymous list but are no longer anonymous: " +
                string.Join(", ", missing));
        }

        [Fact]
        public void Every_Controller_Inherits_The_Authorize_Requirement()
        {
            var offenders = ControllerTypes()
                .Where(t => !typeof(APIBaseController).IsAssignableFrom(t)
                         && t.GetCustomAttribute<AuthorizeAttribute>() is null)
                .Select(t => $"{t.Name} neither derives from APIBaseController nor declares [Authorize].")
                .ToList();

            Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
        }

        [Fact]
        public void The_Base_Controller_Requires_Authorization()
        {
            Assert.NotNull(typeof(APIBaseController).GetCustomAttribute<AuthorizeAttribute>());
        }

        [Fact]
        public void Every_Patient_Data_Endpoint_Requires_Authentication()
        {
            // Named explicitly because these are the PHI endpoints the finding was about.
            var phiControllers = new[]
            {
                typeof(PatientsController), typeof(AppointmentsController),
                typeof(DoctorsController), typeof(ScheduleController)
            };

            foreach (var controller in phiControllers)
            foreach (var action in ActionsOf(controller))
            {
                Assert.False(IsAnonymous(action),
                    $"{controller.Name}.{action.Name} is anonymous and exposes clinical data.");
            }
        }

        [Fact]
        public void A_Fallback_Policy_Requiring_Authentication_Is_Registered()
        {
            // Covers endpoints with no authorization metadata at all - a new minimal API, or a
            // controller that forgets to derive from APIBaseController.
            var services = new ServiceCollection();
            services.AddClinicAuthorization();

            using var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<AuthorizationOptions>>().Value;

            Assert.NotNull(options.FallbackPolicy);
            Assert.Contains(options.FallbackPolicy!.Requirements,
                r => r is DenyAnonymousAuthorizationRequirement);
        }

        [Fact]
        public void The_Coverage_Guard_Actually_Inspects_Some_Actions()
        {
            Assert.True(ControllerTypes().Count() >= 5);
            Assert.True(ControllerTypes().SelectMany(ActionsOf).Count() >= 18);
        }
    }
}
