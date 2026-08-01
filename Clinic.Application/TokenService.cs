using Clinic.Domain.Entites.Identity;
using Clinic.Domain.Service;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Clinic.Application
{
    public class TokenService : ITokenService
    {
        private readonly JwtOptions _jwtOptions;

        // Reads the same validated JwtOptions the bearer handler validates against, so the signing
        // side and the validating side cannot drift apart. Binding also removes the unchecked
        // _configuration["JWT:Key"] null dereference and the double.Parse on ExpireInDays.
        public TokenService(IOptions<JwtOptions> jwtOptions)
        {
            _jwtOptions = jwtOptions.Value;
        }

        public async Task<string> CreateTokenAsync(AppUser user , UserManager<AppUser> userManager)
        {
            // private claims
            var AuthClaim = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.UserName!),
                new Claim(ClaimTypes.NameIdentifier, user.Id)
            };
            // adding role for more encription
            var UserRoles = await userManager.GetRolesAsync(user);
            foreach (var Role in UserRoles)
            {
                AuthClaim.Add(new Claim(ClaimTypes.Role, Role));
            }
            // private key
            var Key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));

            // Token objet
            var Token = new JwtSecurityToken(
                issuer : _jwtOptions.Issuer,
                audience : _jwtOptions.Audience,
                expires : DateTime.UtcNow.AddDays(_jwtOptions.ExpireInDays),
                claims : AuthClaim,
                // SecurityAlgorithms.HmacSha256 is the JWS identifier ("HS256"). The previous
                // HmacSha256Signature constant is the XML-DSig URI; it happened to produce a valid
                // token because JwtHeader translates it through OutboundAlgorithmMap, but it is the
                // wrong constant and it does not match the ValidAlgorithms pin on the validating side.
                signingCredentials : new SigningCredentials(Key, SecurityAlgorithms.HmacSha256)
                );
            return new JwtSecurityTokenHandler().WriteToken(Token);
        }
    }
}
