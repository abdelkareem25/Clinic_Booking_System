using Clinic.Api.Extensions;
using Clinic.Application;
using Clinic.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Clinic.Tests.Extensions
{
    /// <summary>
    /// Regression tests for TODO #3 (finding C3).
    ///
    /// AddJwtBearer() with no arguments binds from the configuration section
    /// "Authentication:Schemes:Bearer", which this application never defined. The handler was left
    /// with IssuerSigningKey == null while ValidateIssuerSigningKey defaulted to true, so no token
    /// could ever validate - while TokenService happily signed with JWT:Key, a key the handler had
    /// never seen.
    /// </summary>
    public sealed class JwtConfigurationTests : IDisposable
    {
        private const string ValidKey = "test-key-that-is-long-enough-for-hmac-sha256";
        private const string ValidIssuer = "https://clinic.test";
        private const string ValidAudience = "ClinicApiUsers";

        private readonly SqliteConnection _connection;

        public JwtConfigurationTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
        }

        private ServiceProvider BuildProvider(Dictionary<string, string?> settings)
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddDbContext<ClinicIdentityDbContext>(o => o.UseSqlite(_connection));
            services.AddIdentityServices(configuration);

            return services.BuildServiceProvider();
        }

        private static Dictionary<string, string?> ValidSettings() => new()
        {
            ["JWT:Key"] = ValidKey,
            ["JWT:Issuer"] = ValidIssuer,
            ["JWT:Audience"] = ValidAudience,
            ["JWT:ExpireInDays"] = "2"
        };

        private TokenValidationParameters ResolveValidationParameters(ServiceProvider provider)
            => provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
                       .Get(JwtBearerDefaults.AuthenticationScheme)
                       .TokenValidationParameters;

        #region TokenValidationParameters are actually populated

        [Fact]
        public void IssuerSigningKey_Is_Configured()
        {
            using var provider = BuildProvider(ValidSettings());

            var parameters = ResolveValidationParameters(provider);

            // This was null before the fix - the root cause of every 401.
            Assert.NotNull(parameters.IssuerSigningKey);

            var symmetric = Assert.IsType<SymmetricSecurityKey>(parameters.IssuerSigningKey);
            Assert.Equal(Encoding.UTF8.GetBytes(ValidKey), symmetric.Key);
        }

        [Fact]
        public void Issuer_And_Audience_Are_Validated_Against_Configuration()
        {
            using var provider = BuildProvider(ValidSettings());

            var parameters = ResolveValidationParameters(provider);

            Assert.True(parameters.ValidateIssuer);
            Assert.Equal(ValidIssuer, parameters.ValidIssuer);
            Assert.True(parameters.ValidateAudience);
            Assert.Equal(ValidAudience, parameters.ValidAudience);
        }

        [Fact]
        public void Signature_And_Lifetime_Validation_Are_Enabled()
        {
            using var provider = BuildProvider(ValidSettings());

            var parameters = ResolveValidationParameters(provider);

            Assert.True(parameters.ValidateIssuerSigningKey);
            Assert.True(parameters.ValidateLifetime);
        }

        [Fact]
        public void ClockSkew_Is_Tightened_From_The_Five_Minute_Default()
        {
            using var provider = BuildProvider(ValidSettings());

            var parameters = ResolveValidationParameters(provider);

            Assert.Equal(TimeSpan.FromSeconds(30), parameters.ClockSkew);
            Assert.True(parameters.ClockSkew < TimeSpan.FromMinutes(5));
        }

        [Fact]
        public void Algorithm_Is_Pinned_To_HS256()
        {
            using var provider = BuildProvider(ValidSettings());

            var parameters = ResolveValidationParameters(provider);

            Assert.NotNull(parameters.ValidAlgorithms);
            Assert.Equal([SecurityAlgorithms.HmacSha256], parameters.ValidAlgorithms);
        }

        #endregion

        #region Options validation fails fast on bad configuration

        [Fact]
        public void Valid_Configuration_Resolves()
        {
            using var provider = BuildProvider(ValidSettings());

            var options = provider.GetRequiredService<IOptions<JwtOptions>>().Value;

            Assert.Equal(ValidKey, options.Key);
            Assert.Equal(2, options.ExpireInDays);
        }

        [Fact]
        public void Missing_Key_Is_Rejected()
        {
            var settings = ValidSettings();
            settings.Remove("JWT:Key");
            using var provider = BuildProvider(settings);

            var ex = Assert.Throws<OptionsValidationException>(
                () => provider.GetRequiredService<IOptions<JwtOptions>>().Value);

            Assert.Contains("JWT:Key is required", string.Join(" ", ex.Failures));
        }

        [Fact]
        public void Short_Key_Is_Rejected()
        {
            // HMAC-SHA256 with a key shorter than the 256-bit hash output is brute-forceable.
            var settings = ValidSettings();
            settings["JWT:Key"] = "too-short";
            using var provider = BuildProvider(settings);

            var ex = Assert.Throws<OptionsValidationException>(
                () => provider.GetRequiredService<IOptions<JwtOptions>>().Value);

            Assert.Contains("at least 32 characters", string.Join(" ", ex.Failures));
        }

        [Fact]
        public void Missing_Issuer_Is_Rejected()
        {
            var settings = ValidSettings();
            settings.Remove("JWT:Issuer");
            using var provider = BuildProvider(settings);

            Assert.Throws<OptionsValidationException>(
                () => provider.GetRequiredService<IOptions<JwtOptions>>().Value);
        }

        [Fact]
        public void Missing_Audience_Is_Rejected()
        {
            var settings = ValidSettings();
            settings.Remove("JWT:Audience");
            using var provider = BuildProvider(settings);

            Assert.Throws<OptionsValidationException>(
                () => provider.GetRequiredService<IOptions<JwtOptions>>().Value);
        }

        [Fact]
        public void Out_Of_Range_Expiry_Is_Rejected()
        {
            var settings = ValidSettings();
            settings["JWT:ExpireInDays"] = "0";
            using var provider = BuildProvider(settings);

            Assert.Throws<OptionsValidationException>(
                () => provider.GetRequiredService<IOptions<JwtOptions>>().Value);
        }

        [Fact]
        public void Bad_Configuration_Also_Breaks_The_Bearer_Handler_Configuration()
        {
            // ConfigureJwtBearerOptions resolves IOptions<JwtOptions>.Value, so an invalid section
            // surfaces here too rather than degrading into a silent 401.
            var settings = ValidSettings();
            settings["JWT:Key"] = "too-short";
            using var provider = BuildProvider(settings);

            Assert.Throws<OptionsValidationException>(() => ResolveValidationParameters(provider));
        }

        #endregion

        public void Dispose() => _connection.Dispose();
    }
}
