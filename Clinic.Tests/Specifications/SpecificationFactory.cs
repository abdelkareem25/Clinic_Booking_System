using Clinic.Domain.Interfaces.Specifications;
using System.Reflection;

namespace Clinic.Tests.Specifications
{
    /// <summary>
    /// Discovers and instantiates every specification in the domain assembly so guard tests cover
    /// specifications added in future without anyone remembering to register them.
    /// </summary>
    internal static class SpecificationFactory
    {
        public static IEnumerable<Type> AllSpecificationTypes() =>
            typeof(BaseSpecification<>).Assembly
                .GetTypes()
                .Where(t => t is { IsAbstract: false, IsGenericTypeDefinition: false, IsClass: true })
                .Where(IsSpecification)
                .OrderBy(t => t.FullName);

        private static bool IsSpecification(Type type)
        {
            for (var current = type.BaseType; current is not null; current = current.BaseType)
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(BaseSpecification<>))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Instantiates a specification through every public constructor it declares, so dead
        /// overloads are validated too - the scalar-include bug lived in three of them.
        /// </summary>
        public static IEnumerable<(ConstructorInfo Ctor, object Instance)> InstantiateAll(Type specificationType)
        {
            foreach (var ctor in specificationType.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
            {
                var arguments = ctor.GetParameters().Select(p => SampleValue(p.ParameterType)).ToArray();
                yield return (ctor, ctor.Invoke(arguments));
            }
        }

        private static object? SampleValue(Type type)
        {
            if (type == typeof(string)) return "sample";
            if (type == typeof(int)) return 1;
            if (type == typeof(int?)) return 1;
            if (type.IsValueType) return Activator.CreateInstance(type);

            var parameterless = type.GetConstructor(Type.EmptyTypes);
            if (parameterless is not null) return parameterless.Invoke(null);

            throw new InvalidOperationException(
                $"SpecificationFactory cannot build a sample value for '{type}'. " +
                "Add support here so the specification guard tests keep covering every specification.");
        }

        /// <summary>Reads the Includes list without knowing the closed generic entity type.</summary>
        public static IReadOnlyList<System.Linq.Expressions.LambdaExpression> IncludesOf(object specification)
        {
            var property = specification.GetType().GetProperty(nameof(ISpecification<Domain.Entites.BaseEntity>.Includes))
                ?? throw new InvalidOperationException("Specification has no Includes property.");

            var value = (System.Collections.IEnumerable?)property.GetValue(specification);

            return value?.Cast<System.Linq.Expressions.LambdaExpression>().ToList() ?? [];
        }
    }
}
