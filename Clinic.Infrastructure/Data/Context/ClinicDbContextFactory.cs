using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Clinic.Infrastructure.Data.Context
{
    /// <summary>
    /// Constructs a <see cref="ClinicDbContext"/> for the EF Core command-line tools.
    ///
    /// Without this, `dotnet ef` can only build a context by starting the API's host, so every
    /// migration command requires --startup-project Clinic.Api and fails outright if the API does
    /// not build. That coupling is why the same command appeared to behave differently depending on
    /// which directory it was run from.
    ///
    /// This is design-time only. Nothing at runtime resolves it - the running application configures
    /// its context through AddDbContext in Program.cs.
    /// </summary>
    public sealed class ClinicDbContextFactory : IDesignTimeDbContextFactory<ClinicDbContext>
    {
        /// <summary>
        /// Matches UserSecretsId in Clinic.Api.csproj. Duplicated rather than referenced because
        /// Infrastructure cannot depend on Api without a cycle; if that id is ever regenerated, this
        /// has to change with it.
        /// </summary>
        private const string ApiUserSecretsId = "414fd82c-50e3-45fc-a3fe-b2536363d79e";

        public ClinicDbContext CreateDbContext(string[] args)
        {
            var configuration = BuildConfiguration();

            // The connection string carries a password and so lives only in user secrets or the
            // environment - see the note in Clinic.Api/appsettings.json. Fail with the same guidance
            // Program.cs gives rather than letting Npgsql report a null argument.
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "ConnectionStrings:DefaultConnection is not configured, so the EF tools cannot " +
                    "construct a ClinicDbContext. Set it with `dotnet user-secrets set " +
                    "\"ConnectionStrings:DefaultConnection\" \"Host=...;Database=...;Username=...;Password=...\"` " +
                    "in the Clinic.Api project, or supply ConnectionStrings__DefaultConnection as an " +
                    "environment variable.");

            var options = new DbContextOptionsBuilder<ClinicDbContext>()
                .UseNpgsql(connectionString)
                .Options;

            // No ICurrentUser and no TimeProvider: design-time work performs no writes, so the audit
            // columns are never stamped. Both parameters are optional for exactly this case.
            return new ClinicDbContext(options);
        }

        /// <summary>
        /// Mirrors the precedence the running host uses - appsettings, then the environment-specific
        /// overlay, then user secrets, then environment variables - so a migration is generated
        /// against the same connection the application would open.
        /// </summary>
        private static IConfiguration BuildConfiguration()
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                ?? "Development";

            var builder = new ConfigurationBuilder();

            // Located by walking up rather than assuming a working directory, because `dotnet ef`
            // may be invoked from the project folder or from the solution folder.
            var apiDirectory = FindApiDirectory();

            if (apiDirectory is not null)
            {
                builder.SetBasePath(apiDirectory)
                       .AddJsonFile("appsettings.json", optional: true)
                       .AddJsonFile($"appsettings.{environment}.json", optional: true);
            }

            // Development-only, exactly as WebApplication.CreateBuilder does it. Loading developer
            // secrets unconditionally would mean a command run against a production environment
            // silently fell back to the developer's local database when the expected environment
            // variable was missing - pointing a migration at the wrong server is not a mistake worth
            // being quiet about.
            if (string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase))
                builder.AddUserSecrets(ApiUserSecretsId);

            return builder
                .AddEnvironmentVariables()
                .Build();
        }

        private static string? FindApiDirectory()
        {
            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, "Clinic.Api");

                if (Directory.Exists(candidate)) return candidate;

                // Covers being invoked from inside Clinic.Api itself.
                if (directory.Name == "Clinic.Api") return directory.FullName;

                directory = directory.Parent;
            }

            return null;
        }
    }
}
