using Clinic.Api.Controllers;
using Clinic.Api.Logging;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace Clinic.Tests.Logging
{
    /// <summary>
    /// Architecture tests for TODO #16 (finding H15).
    ///
    /// Audit coverage is an allow-list in both directions: a controller that handles clinical data
    /// must be marked, and marking one that does not would bury the records that matter under noise.
    /// </summary>
    public sealed class PhiAuditCoverageTests
    {
        /// <summary>Controllers whose data identifies a person as receiving care.</summary>
        private static readonly Dictionary<Type, string> ExpectedPhiControllers = new()
        {
            [typeof(PatientsController)] = "Patient",
            [typeof(AppointmentsController)] = "Appointment"
        };

        /// <summary>
        /// Not PHI. Doctor names and specialisations are a staff directory; a doctor's working hours
        /// say nothing about any patient.
        /// </summary>
        private static readonly Type[] ExpectedNonPhiControllers =
        [
            typeof(DoctorsController),
            typeof(ScheduleController),
            typeof(AccountsController)
        ];

        private static IEnumerable<Type> ControllerTypes() =>
            typeof(APIBaseController).Assembly
                .GetTypes()
                .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

        [Fact]
        public void Every_Clinical_Controller_Is_Marked_For_Audit()
        {
            foreach (var (controller, resourceType) in ExpectedPhiControllers)
            {
                var attribute = controller.GetCustomAttribute<AuditPhiAccessAttribute>();

                Assert.True(attribute is not null,
                    $"{controller.Name} handles protected health information but carries no " +
                    $"[AuditPhiAccess]. Accesses to it would go unrecorded.");

                Assert.Equal(resourceType, attribute!.ResourceType);
            }
        }

        [Fact]
        public void Non_Clinical_Controllers_Are_Not_Marked()
        {
            foreach (var controller in ExpectedNonPhiControllers)
            {
                Assert.True(controller.GetCustomAttribute<AuditPhiAccessAttribute>() is null,
                    $"{controller.Name} is marked for PHI audit but handles no patient data. " +
                    "Auditing everything buries the records that matter.");
            }
        }

        [Fact]
        public void The_Expectations_Cover_Every_Controller()
        {
            // Forces a decision about any newly added controller instead of letting it default to
            // "not audited" by omission.
            var known = ExpectedPhiControllers.Keys.Concat(ExpectedNonPhiControllers).ToHashSet();
            var unclassified = ControllerTypes()
                .Where(t => t != typeof(APIBaseController) && !known.Contains(t))
                .Select(t => t.Name)
                .ToList();

            Assert.True(unclassified.Count == 0,
                "These controllers are classified neither as PHI nor as non-PHI: " +
                string.Join(", ", unclassified) +
                ". Decide, and add them to PhiAuditCoverageTests.");
        }

        [Fact]
        public void The_Audit_Attribute_Is_Inherited_By_Actions()
        {
            // Marking the controller must be enough; requiring it per action would guarantee that
            // someone eventually forgets one.
            var usage = typeof(AuditPhiAccessAttribute).GetCustomAttribute<AttributeUsageAttribute>();

            Assert.NotNull(usage);
            Assert.True(usage!.Inherited);
            Assert.True(usage.ValidOn.HasFlag(AttributeTargets.Class));
            Assert.True(usage.ValidOn.HasFlag(AttributeTargets.Method));
        }
    }
}
