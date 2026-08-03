using Clinic.Api.Extensions;
using Clinic.Domain.Entites.Identity;
using Clinic.Domain.Service;
using Clinic.Infrastructure.Data.Context;
using Clinic.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Clinic.Tests.Extensions
{
    /// <summary>
    /// Regression tests for TODO #2 (finding C2).
    ///
    /// AddIdentity registers the Identity cookie handlers and sets DefaultAuthenticateScheme /
    /// DefaultChallengeScheme to IdentityConstants.ApplicationScheme. Those specific properties win
    /// over DefaultScheme, so the later AddAuthentication("Bearer") had no effect: [Authorize]
    /// authenticated with cookies and ignored the Authorization: Bearer header.
    ///
    /// These tests assert the composed container really selects the JWT bearer handler.
    /// </summary>
    public sealed class IdentityServicesExtensionsTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;

        public IdentityServicesExtensionsTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["JWT:Key"] = "test-key-that-is-long-enough-for-hmac-sha256",
                    ["JWT:Issuer"] = "https://localhost",
                    ["JWT:Audience"] = "ClinicApiUsers",
                    ["JWT:ExpireInDays"] = "1"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
            services.AddSingleton<IConfiguration>(configuration);
            services.AddDbContext<ClinicDbContext>(o => o.UseSqlite(_connection));

            services.AddIdentityServices(configuration); // the system under test

            _provider = services.BuildServiceProvider();
        }

        private IAuthenticationSchemeProvider Schemes =>
            _provider.GetRequiredService<IAuthenticationSchemeProvider>();

        private AuthenticationOptions Options =>
            _provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;

        #region Scheme defaults

        [Fact]
        public void DefaultAuthenticateScheme_Is_Bearer()
            => Assert.Equal(JwtBearerDefaults.AuthenticationScheme, Options.DefaultAuthenticateScheme);

        [Fact]
        public void DefaultChallengeScheme_Is_Bearer()
            => Assert.Equal(JwtBearerDefaults.AuthenticationScheme, Options.DefaultChallengeScheme);

        [Fact]
        public void DefaultScheme_Is_Bearer()
            => Assert.Equal(JwtBearerDefaults.AuthenticationScheme, Options.DefaultScheme);

        [Fact]
        public void DefaultAuthenticateScheme_Is_Not_The_Identity_Cookie_Scheme()
        {
            // The precise regression: AddIdentity used to leave this as Identity.Application.
            Assert.NotEqual(IdentityConstants.ApplicationScheme, Options.DefaultAuthenticateScheme);
            Assert.NotEqual(IdentityConstants.ApplicationScheme, Options.DefaultChallengeScheme);
        }

        #endregion

        #region Resolved handlers

        [Fact]
        public async Task Default_Authenticate_Handler_Is_JwtBearerHandler()
        {
            var scheme = await Schemes.GetDefaultAuthenticateSchemeAsync();

            Assert.NotNull(scheme);
            Assert.Equal(typeof(JwtBearerHandler), scheme!.HandlerType);
        }

        [Fact]
        public async Task Default_Challenge_Handler_Is_JwtBearerHandler()
        {
            var scheme = await Schemes.GetDefaultChallengeSchemeAsync();

            Assert.NotNull(scheme);
            Assert.Equal(typeof(JwtBearerHandler), scheme!.HandlerType);
        }

        [Fact]
        public async Task No_Cookie_Authentication_Handler_Is_Registered()
        {
            var all = await Schemes.GetAllSchemesAsync();

            Assert.DoesNotContain(all, s => s.HandlerType == typeof(CookieAuthenticationHandler));
        }

        [Fact]
        public async Task Identity_Cookie_Schemes_Are_Not_Registered()
        {
            // IdentityConstants members are static readonly, not const, so they cannot be InlineData.
            string[] cookieSchemes =
            [
                IdentityConstants.ApplicationScheme,
                IdentityConstants.ExternalScheme,
                IdentityConstants.TwoFactorUserIdScheme,
                IdentityConstants.TwoFactorRememberMeScheme
            ];

            foreach (var schemeName in cookieSchemes)
                Assert.Null(await Schemes.GetSchemeAsync(schemeName));
        }

        [Fact]
        public async Task Bearer_Scheme_Is_Registered()
        {
            var scheme = await Schemes.GetSchemeAsync(JwtBearerDefaults.AuthenticationScheme);

            Assert.NotNull(scheme);
            Assert.Equal(typeof(JwtBearerHandler), scheme!.HandlerType);
        }

        #endregion

        #region Identity services survive the switch to AddIdentityCore

        // AddIdentityCore registers fewer services than AddIdentity. These assertions prove the
        // pieces the application actually consumes are still present.

        [Fact]
        public void UserManager_Is_Resolvable()
        {
            using var scope = _provider.CreateScope();
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>());
        }

        [Fact]
        public void SignInManager_Is_Resolvable()
        {
            // AccountsController.Login depends on this; AddIdentityCore does not register it,
            // which is why AddSignInManager<>() is called explicitly.
            using var scope = _provider.CreateScope();
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<SignInManager<AppUser>>());
        }

        [Fact]
        public void RoleManager_Is_Resolvable()
        {
            // Needed by role seeding (TODO #11) and by [Authorize(Roles = ...)].
            using var scope = _provider.CreateScope();
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>());
        }

        [Fact]
        public void Role_Store_Is_Registered()
        {
            using var scope = _provider.CreateScope();
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IRoleStore<IdentityRole>>());
        }

        [Fact]
        public void TokenService_Is_Resolvable()
        {
            using var scope = _provider.CreateScope();
            Assert.IsType<Clinic.Application.TokenService>(
                scope.ServiceProvider.GetRequiredService<ITokenService>());
        }

        #endregion

        public void Dispose()
        {
            _provider.Dispose();
            _connection.Dispose();
        }

        private sealed class NullLoggerProvider : ILoggerProvider
        {
            public static readonly NullLoggerProvider Instance = new();
            public ILogger CreateLogger(string categoryName) => Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
            public void Dispose() { }
        }
    }
}
