
using Clinic.Api.Extensions;
using Clinic.Api.Helper;
using Clinic.Api.Logging;
using Clinic.Api.Middleware;
using Clinic.Domain.Entites.Identity;
using Clinic.Domain.Interfaces.Repository;
using Clinic.Infrastructure.Data.Context;
using Clinic.Infrastructure.Identity;
using Clinic.Infrastructure.Repositores;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace Clinic.Api
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Structured logging first, so anything that fails during the rest of startup is
            // actually recorded somewhere.
            builder.Host.AddClinicLogging();

            // Add services to the container.

            builder.Services.AddHttpContextAccessor();
            builder.Services.AddControllers(options =>
            {
                // Records who touched which patient record. Applies only to actions marked with
                // [AuditPhiAccess]; everything else passes straight through.
                options.Filters.Add<PhiAccessAuditFilter>();
            });

            // One error contract for the whole API. AddProblemDetails also gives the automatic
            // [ApiController] model-validation 400s and the bare status codes produced by routing
            // and authorization the same RFC 7807 shape, so a client parses one thing.
            builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
            builder.Services.AddProblemDetails(options =>
            {
                options.CustomizeProblemDetails = context =>
                {
                    context.ProblemDetails.Extensions["traceId"] =
                        Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
                    context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
                };
            });

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddDbContext<ClinicDbContext>(options =>
            {
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
            });
            builder.Services.AddDbContext<ClinicIdentityDbContext>(options =>
            {
                options.UseSqlServer(builder.Configuration.GetConnectionString("IdentityConnection"));
            });
            builder.Services.AddIdentityServices(builder.Configuration);
            builder.Services.AddClinicAuthorization();
            builder.Services.AddClinicRateLimiting(builder.Configuration);
            builder.Services.AddApplicationServices();

            // Allow the Angular dev server (and its https variant) to call the API.
            const string SpaCorsPolicy = "SpaCorsPolicy";
            builder.Services.AddCors(options =>
            {
                options.AddPolicy(SpaCorsPolicy, policy =>
                    policy.WithOrigins("http://localhost:4200", "https://localhost:4200")
                          .AllowAnyHeader()
                          .AllowAnyMethod());
            });

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var userManager = services.GetRequiredService<UserManager<AppUser>>();
                var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
                await ClinicIdentityDbContextSeed.SeedAsync(userManager, roleManager, builder.Configuration);
            }
            // Configure the HTTP request pipeline.

            // Outermost, so it wraps every other middleware. Registered before the Swagger
            // middleware for that reason, and it also takes precedence over the developer exception
            // page WebApplication adds automatically in Development - deliberately, because that
            // page leaks source and configuration to the caller.
            app.UseExceptionHandler();

            // Inside the exception handler so a failed request still produces a completion line,
            // and outside everything else so the timing covers the whole pipeline.
            app.UseClinicRequestLogging();

            // Gives a body to responses that would otherwise be a bare status code: the 404 from
            // routing and the 401/403 from authorization.
            app.UseStatusCodePages();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseCors(SpaCorsPolicy);
            app.UseAuthentication();
            app.UseAuthorization();

            // After routing (added automatically) so per-endpoint [EnableRateLimiting] policies
            // resolve from endpoint metadata.
            app.UseRateLimiter();


            app.MapControllers();

            app.Run();
        }
    }
}