using Clinic.Domain.Entites;
using Clinic.Domain.Interfaces.Specifications.AppointmentSpec;
using Clinic.Domain.Interfaces.Specifications.PatientSpec;
using Clinic.Domain.Interfaces.Specifications.ScheduleSpec;
using System.Collections;
using System.Linq.Expressions;
using System.Reflection;

namespace Clinic.Tests.Specifications
{
    /// <summary>
    /// Regression tests for TODO #5 (finding C5).
    ///
    /// Includes is typed as Expression&lt;Func&lt;T, object&gt;&gt;, so a scalar member such as
    /// a => a.Doctor.Name compiles cleanly - string boxes to object. The compiler cannot help here,
    /// and EF Core only rejects it at query time. These tests take the compiler's place.
    /// </summary>
    public sealed class SpecificationIncludeTests
    {
        #region Guard applied to every specification in the assembly

        [Fact]
        public void Every_Include_In_Every_Specification_Targets_A_Navigation_Property()
        {
            var violations = new List<string>();

            foreach (var specificationType in SpecificationFactory.AllSpecificationTypes())
            {
                foreach (var (ctor, instance) in SpecificationFactory.InstantiateAll(specificationType))
                {
                    foreach (var include in SpecificationFactory.IncludesOf(instance))
                    {
                        var body = Unwrap(include.Body);

                        if (body is not MemberExpression member)
                        {
                            violations.Add($"{Describe(specificationType, ctor)}: include '{include}' " +
                                           "is not a property access.");
                            continue;
                        }

                        var memberType = MemberType(member.Member);

                        if (!IsNavigation(memberType))
                        {
                            violations.Add($"{Describe(specificationType, ctor)}: include '{include}' " +
                                           $"targets scalar member '{member.Member.Name}' of type " +
                                           $"'{memberType.Name}'. Include navigation properties only.");
                        }
                    }
                }
            }

            Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
        }

        [Fact]
        public void The_Guard_Actually_Inspects_Some_Specifications()
        {
            // Protects against the guard above silently passing because discovery found nothing.
            var specifications = SpecificationFactory.AllSpecificationTypes().ToList();

            Assert.True(specifications.Count >= 10,
                $"Expected the domain assembly to expose at least 10 specifications, found {specifications.Count}.");

            var totalIncludes = specifications
                .SelectMany(SpecificationFactory.InstantiateAll)
                .Sum(x => SpecificationFactory.IncludesOf(x.Instance).Count);

            Assert.True(totalIncludes > 0, "No includes were inspected; the guard would be vacuous.");
        }

        #endregion

        #region The three specifications that carried the bug

        [Fact]
        public void AppointmentWithDoctorAndPatientSpec_Includes_Both_Navigations()
        {
            AssertIncludes(new AppointmentWithDoctorAndPatientSpec(1),
                nameof(Appointment.Doctor), nameof(Appointment.Patient));

            AssertIncludes(new AppointmentWithDoctorAndPatientSpec(),
                nameof(Appointment.Doctor), nameof(Appointment.Patient));
        }

        [Fact]
        public void AppointmentWithDoctorNameSpec_Includes_Both_Navigations()
        {
            // The DTO exposes DoctorName and PatientName, so both must be loaded.
            AssertIncludes(new AppointmentWithDoctorNameSpec("Dr. Aya"),
                nameof(Appointment.Doctor), nameof(Appointment.Patient));

            AssertIncludes(new AppointmentWithDoctorNameSpec(), nameof(Appointment.Doctor));
        }

        [Fact]
        public void AppointmentWithPatientNameSpec_Includes_Both_Navigations()
        {
            AssertIncludes(new AppointmentWithPatientNameSpec("Sara"),
                nameof(Appointment.Patient), nameof(Appointment.Doctor));

            AssertIncludes(new AppointmentWithPatientNameSpec(),
                nameof(Appointment.Patient), nameof(Appointment.Doctor));
        }

        [Fact]
        public void Specifications_That_Were_Already_Correct_Still_Include_Their_Navigations()
        {
            AssertIncludes(new ScheduleSpecification(new DoctorScheduleSpecParams()),
                nameof(DoctorSchedule.Doctor));

            AssertIncludes(new PatientsWithAppointmentsSpecification(),
                nameof(Patient.Appointments));
        }

        [Fact]
        public void Criteria_May_Still_Reference_Scalar_Members_Of_A_Navigation()
        {
            // a.Doctor.Name in a WHERE clause is perfectly valid - only Include is restricted.
            var spec = new AppointmentWithDoctorNameSpec("Dr. Aya");

            Assert.NotNull(spec.Criteria);
            Assert.Contains("Doctor.Name", spec.Criteria!.ToString());
        }

        #endregion

        #region Helpers

        private static void AssertIncludes(
            Clinic.Domain.Interfaces.Specifications.ISpecification<Appointment> spec,
            params string[] expected)
            => AssertIncludeNames(spec.Includes.Cast<LambdaExpression>(), expected);

        private static void AssertIncludes(
            Clinic.Domain.Interfaces.Specifications.ISpecification<DoctorSchedule> spec,
            params string[] expected)
            => AssertIncludeNames(spec.Includes.Cast<LambdaExpression>(), expected);

        private static void AssertIncludes(
            Clinic.Domain.Interfaces.Specifications.ISpecification<Patient> spec,
            params string[] expected)
            => AssertIncludeNames(spec.Includes.Cast<LambdaExpression>(), expected);

        private static void AssertIncludeNames(IEnumerable<LambdaExpression> includes, string[] expected)
        {
            var actual = includes
                .Select(i => Unwrap(i.Body))
                .OfType<MemberExpression>()
                .Select(m => m.Member.Name)
                .ToList();

            Assert.Equal(expected.Length, actual.Count);
            foreach (var name in expected) Assert.Contains(name, actual);
        }

        private static Expression Unwrap(Expression expression) =>
            expression is UnaryExpression { NodeType: ExpressionType.Convert } unary
                ? unary.Operand
                : expression;

        private static Type MemberType(MemberInfo member) => member switch
        {
            PropertyInfo p => p.PropertyType,
            FieldInfo f => f.FieldType,
            _ => typeof(object)
        };

        /// <summary>An entity, or a collection of entities. Anything else EF rejects in Include.</summary>
        private static bool IsNavigation(Type type)
        {
            if (typeof(BaseEntity).IsAssignableFrom(type)) return true;

            if (typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string))
            {
                var elementType = type.IsGenericType ? type.GetGenericArguments().FirstOrDefault() : null;
                return elementType is not null && typeof(BaseEntity).IsAssignableFrom(elementType);
            }

            return false;
        }

        private static string Describe(Type specificationType, ConstructorInfo ctor) =>
            $"{specificationType.Name}({string.Join(", ", ctor.GetParameters().Select(p => p.ParameterType.Name))})";

        #endregion
    }
}
