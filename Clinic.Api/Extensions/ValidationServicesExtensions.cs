using Clinic.Api.DTOs.DoctorDto.Validators;
using FluentValidation;

namespace Clinic.Api.Extensions
{
    public static class ValidationServicesExtensions
    {
        /// <summary>
        /// Discovers every AbstractValidator in the API assembly and registers it.
        ///
        /// Scanning rather than listing them one by one: a validator that has to be remembered in a
        /// registration list is a validator that eventually is not. That is precisely how five of
        /// them came to exist without ever running.
        /// </summary>
        public static IServiceCollection AddClinicValidation(this IServiceCollection services)
        {
            services.AddValidatorsFromAssemblyContaining<CreateDoctorDtoValidator>(
                lifetime: ServiceLifetime.Scoped,
                includeInternalTypes: true);

            return services;
        }
    }
}
