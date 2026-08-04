using Clinic.Api.Helper;
using Clinic.Api.Services;
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
            // IAccountRepository lives in AddIdentityServices - see the note there.
            Services.AddAutoMapper(config=>config.AddProfile<MappingProfile>());

            // Consumed by ClinicDbContext to stamp the audit columns. Scoped, because "who" is a
            // per-request fact.
            Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

            // .NET 8's built-in clock abstraction. Registering it makes time injectable, so audit
            // timestamps are assertable in tests rather than being whatever DateTime.UtcNow said.
            Services.AddSingleton(TimeProvider.System);

            return Services;
        }
    }
}
