using Clinic.Api.Helper;
using Clinic.Domain.Interfaces;
using Clinic.Domain.Interfaces.Repository;
using Clinic.Infrastructure.Repositores;

namespace Clinic.Api.Extensions
{
    public static class ApplicationServicesExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection Services)
        {
            Services.AddScoped<IUnitOfWork, UnitOfWork>();
            Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            Services.AddScoped<IAppointmentRepository, AppointmentRepository>();
            Services.AddAutoMapper(config=>config.AddProfile<MappingProfile>());

            return Services;
        }
    }
}
