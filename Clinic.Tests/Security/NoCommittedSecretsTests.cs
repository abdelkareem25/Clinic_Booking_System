using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace Clinic.Tests.Security
{
    /// <summary>
    /// Regression tests for TODO #9 (finding C11).
    ///
    /// The JWT signing key and the seed administrator password were literals in tracked files. The
    /// signing key is the root of trust for authentication: anyone holding it can mint a token for
    /// any user with any role, so a leaked key is a total authentication bypass.
    ///
    /// These tests scan the actual files on disk rather than any in-memory abstraction, so they fail
    /// the moment a secret is pasted back into configuration or source.
    /// </summary>
    public sealed class NoCommittedSecretsTests
    {
        /// <summary>Literals that were committed and must never reappear.</summary>
        private static readonly string[] BurnedSecrets =
        [
            "MySuperSecretKeyForJWTTokenGeneration",
            "Password123!"
        ];

        private static readonly string[] SecretConfigurationKeys =
        [
            "JWT:Key",
            "Seed:AdminPassword"
        ];

        private static DirectoryInfo SolutionRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "Clinic.Api")))
                directory = directory.Parent;

            Assert.True(directory is not null, "Could not locate the solution root from the test output directory.");
            return directory!;
        }

        private static IEnumerable<FileInfo> TrackedTextFiles() =>
            SolutionRoot()
                .EnumerateFiles("*.*", SearchOption.AllDirectories)
                .Where(f => f.Extension is ".cs" or ".json" or ".csproj" or ".config" or ".yml" or ".yaml")
                .Where(f => !f.FullName.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                         && !f.FullName.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                         && !f.FullName.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}")
                         && !f.FullName.Contains($"{Path.DirectorySeparatorChar}.vs{Path.DirectorySeparatorChar}")
                         && !f.FullName.Contains($"{Path.DirectorySeparatorChar}.angular{Path.DirectorySeparatorChar}")
                         && !f.FullName.Contains($"{Path.DirectorySeparatorChar}dist{Path.DirectorySeparatorChar}"));

        private static IEnumerable<FileInfo> AppSettingsFiles() =>
            SolutionRoot()
                .EnumerateFiles("appsettings*.json", SearchOption.AllDirectories)
                .Where(f => !f.FullName.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                         && !f.FullName.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

        [Fact]
        public void No_Source_Or_Configuration_File_Contains_A_Previously_Committed_Secret()
        {
            var offenders = new List<string>();

            foreach (var file in TrackedTextFiles())
            {
                // This test file legitimately names the burned values.
                if (file.Name == $"{nameof(NoCommittedSecretsTests)}.cs") continue;

                var content = File.ReadAllText(file.FullName);

                foreach (var secret in BurnedSecrets)
                {
                    if (content.Contains(secret, StringComparison.Ordinal))
                        offenders.Add($"{Path.GetRelativePath(SolutionRoot().FullName, file.FullName)} " +
                                      $"contains the burned secret '{secret}'.");
                }
            }

            Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
        }

        [Fact]
        public void No_AppSettings_File_Declares_A_Secret_Configuration_Key()
        {
            var offenders = new List<string>();

            foreach (var file in AppSettingsFiles())
            {
                var configuration = new ConfigurationBuilder()
                    .AddJsonFile(file.FullName, optional: false)
                    .Build();

                foreach (var key in SecretConfigurationKeys)
                {
                    if (!string.IsNullOrEmpty(configuration[key]))
                        offenders.Add($"{Path.GetRelativePath(SolutionRoot().FullName, file.FullName)} " +
                                      $"defines '{key}'. Secrets belong in user-secrets, an environment " +
                                      "variable, or a managed secret store - never in a tracked file.");
                }
            }

            Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
        }

        /// <summary>
        /// The database password was committed in appsettings.json long after JWT:Key and
        /// Seed:AdminPassword had been moved out, because the checks above enumerate specific keys
        /// and ConnectionStrings was not one of them. A connection string is credential-bearing by
        /// nature, so this inspects the section itself rather than naming another key to forget.
        /// </summary>
        [Fact]
        public void No_AppSettings_File_Declares_A_Connection_String_Containing_A_Password()
        {
            string[] credentialKeywords = ["password=", "pwd=", "user id=", "username="];

            var offenders = new List<string>();

            foreach (var file in AppSettingsFiles())
            {
                var configuration = new ConfigurationBuilder()
                    .AddJsonFile(file.FullName, optional: false)
                    .Build();

                foreach (var entry in configuration.GetSection("ConnectionStrings").GetChildren())
                {
                    var value = entry.Value;

                    if (string.IsNullOrWhiteSpace(value)) continue;

                    foreach (var keyword in credentialKeywords)
                    {
                        if (value.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                            offenders.Add($"{Path.GetRelativePath(SolutionRoot().FullName, file.FullName)} " +
                                          $"defines connection string '{entry.Key}' containing '{keyword}'. " +
                                          "Connection strings with credentials belong in user-secrets or " +
                                          "an environment variable, never in a tracked file.");
                    }
                }
            }

            Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
        }

        [Fact]
        public void The_Api_AppSettings_Still_Provides_The_Non_Secret_Jwt_Settings()
        {
            // Removing the key must not remove the rest of the section - and this also proves the
            // explanatory // comments in appsettings.json parse (JsonConfigurationProvider skips them).
            var path = Path.Combine(SolutionRoot().FullName, "Clinic.Api", "appsettings.json");

            var configuration = new ConfigurationBuilder().AddJsonFile(path, optional: false).Build();

            Assert.Null(configuration["JWT:Key"]);
            Assert.Equal("ClinicApiUsers", configuration["JWT:Audience"]);
            Assert.False(string.IsNullOrWhiteSpace(configuration["JWT:Issuer"]));
            Assert.False(string.IsNullOrWhiteSpace(configuration["JWT:ExpireInDays"]));
        }

        [Fact]
        public void The_Api_AppSettings_Is_Still_Valid_Json()
        {
            var path = Path.Combine(SolutionRoot().FullName, "Clinic.Api", "appsettings.json");

            using var document = JsonDocument.Parse(
                File.ReadAllText(path),
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

            Assert.True(document.RootElement.TryGetProperty("JWT", out _));
        }

        [Fact]
        public void The_Api_Project_Has_A_UserSecretsId()
        {
            // Without it, `dotnet user-secrets` has nowhere to store values and the documented
            // local-development workflow silently does nothing.
            var csproj = File.ReadAllText(
                Path.Combine(SolutionRoot().FullName, "Clinic.Api", "Clinic.Api.csproj"));

            Assert.Contains("<UserSecretsId>", csproj, StringComparison.Ordinal);
        }

        [Fact]
        public void Gitignore_Covers_The_Usual_Secret_Carriers()
        {
            var gitignore = File.ReadAllText(Path.Combine(SolutionRoot().FullName, ".gitignore"));

            foreach (var pattern in new[] { "secrets.json", "appsettings.Development.json", ".env", "*.pfx", "*.key" })
                Assert.Contains(pattern, gitignore, StringComparison.Ordinal);
        }

        [Fact]
        public void The_Scanner_Actually_Reads_Some_Files()
        {
            // Protects against the guards above passing because the directory walk found nothing.
            Assert.True(TrackedTextFiles().Count() > 50);
            Assert.True(AppSettingsFiles().Any());
        }
    }
}
