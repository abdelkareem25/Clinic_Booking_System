using Clinic.Application;
using Clinic.Domain.Entites.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Clinic.Tests.Application
{
    /// <summary>
    /// Unit tests for TODO #3 (finding C3), covering the signing half of the contract.
    /// The critical assertion is the round trip: a token produced by TokenService must survive the
    /// exact TokenValidationParameters the API installs on the bearer handler.
    /// </summary>
    public sealed class TokenServiceTests
    {
        private const string Key = "test-key-that-is-long-enough-for-hmac-sha256";
        private const string Issuer = "https://clinic.test";
        private const string Audience = "ClinicApiUsers";

        private static readonly AppUser User = new()
        {
            Id = "user-1",
            UserName = "aya",
            Email = "aya@clinic.test",
            DisplayName = "Dr. Aya"
        };

        private static JwtOptions OptionsWith(
            string key = Key, string issuer = Issuer, string audience = Audience, int expireInDays = 2)
            => new() { Key = key, Issuer = issuer, Audience = audience, ExpireInDays = expireInDays };

        private static TokenService SutWith(JwtOptions options)
            => new(Microsoft.Extensions.Options.Options.Create(options));

        private static UserManager<AppUser> UserManagerWithRoles(params string[] roles)
        {
            var store = new Mock<IUserStore<AppUser>>();
            var userManager = new Mock<UserManager<AppUser>>(
                store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
            userManager.Setup(m => m.GetRolesAsync(It.IsAny<AppUser>())).ReturnsAsync(roles);
            return userManager.Object;
        }

        private static JwtSecurityToken Decode(string token)
            => new JwtSecurityTokenHandler().ReadJwtToken(token);

        /// <summary>The exact parameters ConfigureJwtBearerOptions installs on the handler.</summary>
        private static TokenValidationParameters ApiValidationParameters() => new()
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
        };

        [Fact]
        public async Task Token_Is_Signed_With_The_HS256_Jws_Algorithm()
        {
            // Guards the ValidAlgorithms pin. The old HmacSha256Signature constant is the XML-DSig
            // URI; it only worked because JwtHeader translates it via OutboundAlgorithmMap.
            var jwt = Decode(await SutWith(OptionsWith()).CreateTokenAsync(User, UserManagerWithRoles()));

            Assert.Equal(SecurityAlgorithms.HmacSha256, jwt.Header.Alg);
            Assert.Equal("HS256", jwt.Header.Alg);
        }

        [Fact]
        public async Task Token_Carries_The_Configured_Issuer_And_Audience()
        {
            var jwt = Decode(await SutWith(OptionsWith()).CreateTokenAsync(User, UserManagerWithRoles()));

            Assert.Equal(Issuer, jwt.Issuer);
            Assert.Contains(Audience, jwt.Audiences);
        }

        [Fact]
        public async Task Token_Carries_Identity_Claims()
        {
            var token = await SutWith(OptionsWith()).CreateTokenAsync(User, UserManagerWithRoles());

            var principal = new JwtSecurityTokenHandler()
                .ValidateToken(token, ApiValidationParameters(), out _);

            Assert.Equal("aya", principal.FindFirstValue(ClaimTypes.Name));
            Assert.Equal("user-1", principal.FindFirstValue(ClaimTypes.NameIdentifier));
        }

        [Fact]
        public async Task Token_Carries_Role_Claims()
        {
            var token = await SutWith(OptionsWith())
                .CreateTokenAsync(User, UserManagerWithRoles("Admin", "Doctor"));

            var principal = new JwtSecurityTokenHandler()
                .ValidateToken(token, ApiValidationParameters(), out _);

            Assert.True(principal.IsInRole("Admin"));
            Assert.True(principal.IsInRole("Doctor"));
            Assert.False(principal.IsInRole("Receptionist"));
        }

        [Fact]
        public async Task Token_Expires_After_The_Configured_Number_Of_Days()
        {
            var options = OptionsWith(expireInDays: 2);

            var jwt = Decode(await SutWith(options).CreateTokenAsync(User, UserManagerWithRoles()));

            var expected = DateTime.UtcNow.AddDays(options.ExpireInDays);
            Assert.True((jwt.ValidTo - expected).Duration() < TimeSpan.FromMinutes(1),
                $"Expected expiry near {expected:O} but got {jwt.ValidTo:O}.");
        }

        [Fact]
        public async Task Token_Validates_Against_The_Parameters_The_Api_Installs()
        {
            // The end of the C3 defect: signing side and validating side now agree.
            var token = await SutWith(OptionsWith()).CreateTokenAsync(User, UserManagerWithRoles("Admin"));

            var principal = new JwtSecurityTokenHandler()
                .ValidateToken(token, ApiValidationParameters(), out var validated);

            Assert.NotNull(principal.Identity);
            Assert.True(principal.Identity!.IsAuthenticated);
            Assert.Equal(SecurityAlgorithms.HmacSha256, ((JwtSecurityToken)validated).Header.Alg);
        }

        [Fact]
        public async Task Token_Signed_With_A_Different_Key_Is_Rejected()
        {
            var token = await SutWith(OptionsWith(key: "a-completely-different-key-also-long-enough"))
                .CreateTokenAsync(User, UserManagerWithRoles());

            Assert.Throws<SecurityTokenSignatureKeyNotFoundException>(
                () => new JwtSecurityTokenHandler().ValidateToken(token, ApiValidationParameters(), out _));
        }

        [Fact]
        public async Task Token_With_A_Different_Issuer_Is_Rejected()
        {
            var token = await SutWith(OptionsWith(issuer: "https://evil.example"))
                .CreateTokenAsync(User, UserManagerWithRoles());

            Assert.Throws<SecurityTokenInvalidIssuerException>(
                () => new JwtSecurityTokenHandler().ValidateToken(token, ApiValidationParameters(), out _));
        }

        [Fact]
        public async Task Token_With_A_Different_Audience_Is_Rejected()
        {
            var token = await SutWith(OptionsWith(audience: "SomeOtherApi"))
                .CreateTokenAsync(User, UserManagerWithRoles());

            Assert.Throws<SecurityTokenInvalidAudienceException>(
                () => new JwtSecurityTokenHandler().ValidateToken(token, ApiValidationParameters(), out _));
        }

        [Fact]
        public async Task Token_With_A_Tampered_Signature_Is_Rejected()
        {
            var token = await SutWith(OptionsWith()).CreateTokenAsync(User, UserManagerWithRoles());

            // Alter the signature segment only. Header and payload stay well-formed, so the handler
            // gets far enough to actually verify the signature rather than failing to parse.
            var segments = token.Split('.');
            var signature = segments[2];
            segments[2] = (signature[0] == 'a' ? 'b' : 'a') + signature[1..];
            var tampered = string.Join('.', segments);

            // The handler reports this as SecurityTokenSignatureKeyNotFoundException rather than
            // SecurityTokenInvalidSignatureException; both derive from SecurityTokenValidationException,
            // and what matters is that signature verification refuses the token.
            var ex = Assert.ThrowsAny<SecurityTokenValidationException>(
                () => new JwtSecurityTokenHandler().ValidateToken(tampered, ApiValidationParameters(), out _));

            Assert.Contains("Signature validation failed", ex.Message);
        }

        [Fact]
        public async Task Token_With_A_Tampered_Payload_Is_Rejected()
        {
            var token = await SutWith(OptionsWith()).CreateTokenAsync(User, UserManagerWithRoles());

            var segments = token.Split('.');
            segments[1] = segments[1][..^2] + (segments[1][^2] == 'A' ? 'B' : 'A') + segments[1][^1];
            var tampered = string.Join('.', segments);

            // A mangled payload may fail to parse before signature verification is reached; either
            // way it must never validate.
            Assert.ThrowsAny<Exception>(
                () => new JwtSecurityTokenHandler().ValidateToken(tampered, ApiValidationParameters(), out _));
        }
    }
}
