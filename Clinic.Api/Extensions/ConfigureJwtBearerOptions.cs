using Clinic.Application;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Clinic.Api.Extensions
{
    /// <summary>
    /// Supplies the bearer handler's <see cref="TokenValidationParameters"/> from the validated
    /// <see cref="JwtOptions"/>.
    ///
    /// The parameterless AddJwtBearer() overload binds from the configuration section
    /// "Authentication:Schemes:Bearer", which this application does not define. The handler was
    /// therefore left with IssuerSigningKey == null while ValidateIssuerSigningKey defaulted to
    /// true, so signature validation threw SecurityTokenSignatureKeyNotFoundException for every
    /// request and no token could ever be accepted.
    /// </summary>
    internal sealed class ConfigureJwtBearerOptions : IConfigureNamedOptions<JwtBearerOptions>
    {
        private readonly JwtOptions _jwt;

        public ConfigureJwtBearerOptions(IOptions<JwtOptions> jwtOptions)
            => _jwt = jwtOptions.Value; // resolving .Value here triggers DataAnnotations validation

        public void Configure(string? name, JwtBearerOptions options)
        {
            if (name != JwtBearerDefaults.AuthenticationScheme) return;
            Configure(options);
        }

        public void Configure(JwtBearerOptions options)
        {
            // Do not stash the raw token on the AuthenticationProperties; nothing needs it and it
            // would end up in any serialized auth ticket.
            options.SaveToken = false;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _jwt.Issuer,

                ValidateAudience = true,
                ValidAudience = _jwt.Audience,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key)),

                ValidateLifetime = true,

                // The 5-minute default keeps expired tokens usable for far too long.
                ClockSkew = TimeSpan.FromSeconds(30),

                // Pin the algorithm. Without this an attacker can attempt algorithm-confusion
                // attacks by re-signing the token with a different alg header.
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
            };
        }
    }
}
