using Clinic.Api.Helper;
using Clinic.Api.Services;
using Clinic.Domain.Interfaces;
using Clinic.Domain.Interfaces.Repository;
using Clinic.Infrastructure.Repositores;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

            // Both HttpContextCurrentUser and HttpContextCurrentTenant read the current request, so
            // this extension registers the accessor they need rather than assuming the host did.
            // Program.cs also calls it; it is a TryAdd internally, so calling it twice is free.
            //
            // Without this the two registrations below resolve only in a host that happens to have
            // called AddHttpContextAccessor first - the same "stands up, then fails to activate"
            // trap documented next to IAccountRepository in AddIdentityServices.
            Services.AddHttpContextAccessor();

            // Consumed by ClinicDbContext to stamp the audit columns. Scoped, because "who" is a
            // per-request fact.
            Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

            // MULTI-TENANT: consumed by ClinicDbContext to filter every query and to stamp every
            // insert. Scoped for the same reason ICurrentUser is - "which clinic" is a per-request
            // fact, and a singleton would pin the first request's tenant for the process lifetime.
            //
            // TryAdd because AddIdentityServices registers it too, so that a host wiring only
            // identity can still activate AccountsController; Program.cs calls both.
            Services.TryAddScoped<ICurrentTenant, HttpContextCurrentTenant>();

            // .NET 8's built-in clock abstraction. Registering it makes time injectable, so audit
            // timestamps are assertable in tests rather than being whatever DateTime.UtcNow said.
            Services.AddSingleton(TimeProvider.System);

            return Services;
        }
    }
}
