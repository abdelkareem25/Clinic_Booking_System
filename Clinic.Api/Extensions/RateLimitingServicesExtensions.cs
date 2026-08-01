using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace Clinic.Api.Extensions
{
    public static class RateLimitingServicesExtensions
    {
        /// <summary>Applied to the credential-accepting endpoints with [EnableRateLimiting].</summary>
        public const string AuthPolicy = "auth";

        public const int DefaultAuthPermitLimit = 5;
        public const int DefaultAuthWindowMinutes = 1;

        /// <summary>
        /// Account lockout alone is not enough. It is per-account, so it does nothing against an
        /// attacker spraying one common password across many accounts, and nothing against the cost
        /// of the password hash itself - every attempt burns a PBKDF2 computation on the server,
        /// which is a cheap denial-of-service lever.
        ///
        /// The limiter is partitioned by client address, so one attacker cannot exhaust the budget
        /// for everyone else - a global limiter on a login endpoint is itself a denial-of-service.
        ///
        /// The thresholds come from configuration because they are an operational decision: the
        /// right number differs between a clinic behind a corporate NAT (where many staff share one
        /// address) and a public deployment.
        /// </summary>
        public static IServiceCollection AddClinicRateLimiting(
            this IServiceCollection services, IConfiguration configuration)
        {
            var permitLimit = configuration.GetValue("RateLimiting:Auth:PermitLimit", DefaultAuthPermitLimit);
            var windowMinutes = configuration.GetValue("RateLimiting:Auth:WindowMinutes", DefaultAuthWindowMinutes);

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                options.AddPolicy(AuthPolicy, httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            Window = TimeSpan.FromMinutes(windowMinutes),
                            PermitLimit = permitLimit,

                            // Reject immediately rather than queueing. Queuing a brute-force attempt
                            // just holds a server thread open on the attacker's behalf.
                            QueueLimit = 0
                        }));

                // Tell a rejected caller when to come back, so a legitimate user who tripped the
                // limit is not left guessing.
                options.OnRejected = (context, cancellationToken) =>
                {
                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    {
                        context.HttpContext.Response.Headers.RetryAfter =
                            ((int)retryAfter.TotalSeconds).ToString();
                    }

                    return ValueTask.CompletedTask;
                };
            });

            return services;
        }
    }
}
