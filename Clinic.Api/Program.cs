
using Clinic.Api.Extensions;
using Clinic.Api.Helper;
using Clinic.Api.Logging;
using Clinic.Api.Middleware;
using Clinic.Api.Validation;
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

                // Runs the FluentValidation validators. The explicit order places it ahead of
                // [ApiController]'s ModelStateInvalidFilter so its failures join the DataAnnotation
                // ones in a single ValidationProblemDetails.
                options.Filters.Add<FluentValidationActionFilter>(FluentValidationActionFilter.FilterOrder);
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
            // One context, one database. Identity and clinical records were split across two,
            // which made any operation spanning both impossible to commit atomically and made a
            // foreign key from a Patient to their login impossible to express. See TODO #23.
            // Read once and checked here rather than left to Npgsql. The connection string carries a
            // password, so it is supplied by user-secrets or the environment and is absent from
            // every tracked file - which means "not configured" is a realistic mistake and deserves
            // an error that says what to do. Passing null through to UseNpgsql instead produces an
            // ArgumentNullException naming a parameter, which tells nobody anything.
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "ConnectionStrings:DefaultConnection is not configured. Set it with " +
                    "`dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"Host=...;Database=...;Username=...;Password=...\"` " +
                    "for local development, or supply ConnectionStrings__DefaultConnection as an " +
                    "environment variable.");

            builder.Services.AddDbContext<ClinicDbContext>(options =>
            {
                options.UseNpgsql(connectionString);
            });
            builder.Services.AddIdentityServices(builder.Configuration);
            builder.Services.AddClinicAuthorization();
            builder.Services.AddClinicRateLimiting(builder.Configuration);
            builder.Services.AddClinicValidation();
            builder.Services.AddApplicationServices();

            // Allow the Angular SPA to call the API.
            //
            // Origins come from configuration (Cors:AllowedOrigins) so a deployment overrides them
            // with Cors__AllowedOrigins__0 etc. rather than needing a code change; the Angular dev
            // server's two localhost origins are the fallback for a fresh clone.
            //
            // Note what is deliberately absent: AllowCredentials. The SPA authenticates with a
            // bearer token in the Authorization header, not a cookie, so credentialed CORS buys
            // nothing and would forbid the wildcard fallbacks below.
            const string SpaCorsPolicy = "SpaCorsPolicy";
            var allowedOrigins = builder.Configuration
                .GetSection("Cors:AllowedOrigins")
                .Get<string[]>() ?? ["http://localhost:4200", "https://localhost:4200"];

            builder.Services.AddCors(options =>
            {
                options.AddPolicy(SpaCorsPolicy, policy =>
                    policy.WithOrigins(allowedOrigins)
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          // The SPA reads nothing custom off the response today, but exposing
                          // these keeps pagination and correlation usable from the browser.
                          .WithExposedHeaders("Content-Disposition", "X-Pagination")
                          .SetPreflightMaxAge(TimeSpan.FromMinutes(10)));
            });

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var logger = services.GetRequiredService<ILogger<Program>>();

                try
                {
                    // Schema before data. Seeding is the first thing that touches AspNetRoles, so
                    // without this the very first query fails with 42P01 (relation does not exist)
                    // against any database that has not been migrated by hand. Applying migrations
                    // here means a clean database is a working database.
                    //
                    // Opt-out rather than opt-in: a deployment that moves migration into a release
                    // step sets Database:MigrateOnStartup=false, which is a deliberate act. The
                    // reverse default would let a misconfigured environment start against an empty
                    // schema and fail later with a much less obvious error.
                    if (builder.Configuration.GetValue("Database:MigrateOnStartup", true))
                    {
                        var context = services.GetRequiredService<ClinicDbContext>();
                        await context.Database.MigrateAsync();
                    }

                    var userManager = services.GetRequiredService<UserManager<AppUser>>();
                    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
                    var configuration = services.GetRequiredService<IConfiguration>();

                    await ClinicIdentityDbContextSeed.SeedAsync(userManager, roleManager, configuration);
                }
                catch (Exception ex)
                {
                    // Without this the failure surfaces as an unhandled exception from Main, which
                    // bypasses the configured logging sinks entirely - so the one startup failure
                    // that matters is the one nothing records.
                    logger.LogCritical(ex, "Database migration or identity seeding failed. The application cannot start.");
                    throw;
                }
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

            // Explicit, because UseCors must run after routing for per-endpoint CORS metadata to
            // resolve. Left implicit it is inserted at the top of the pipeline, which happens to
            // work, but stating it makes the ordering requirement below visible.
            app.UseRouting();

            // CORS sits as far out as it legally can, and specifically AHEAD of the three
            // middlewares that can end a request early:
            //
            //   UseHttpsRedirection - a preflight OPTIONS answered with a 307 is a failed
            //                         preflight; browsers do not follow redirects on preflight.
            //   UseStatusCodePages  - a 401/403/404 without Access-Control-Allow-Origin surfaces
            //                         in the browser as an opaque CORS error rather than the
            //                         status the API actually returned, which is what makes a
            //                         bad-password response look like a CORS bug.
            //   UseRateLimiter      - a throttled 429 has the same problem, and a preflight must
            //                         never consume the login rate-limit budget.
            //
            // The middleware also short-circuits preflight itself, so OPTIONS never reaches
            // authentication or the endpoint at all.
            app.UseCors(SpaCorsPolicy);

            // Gives a body to responses that would otherwise be a bare status code: the 404 from
            // routing and the 401/403 from authorization.
            app.UseStatusCodePages();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
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