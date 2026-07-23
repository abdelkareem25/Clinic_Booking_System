using Clinic.Domain.Entites.Identity;
using Clinic.Domain.Service;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Clinic.Application
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;

        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public async Task<string> CreateTokenAsync(AppUser user , UserManager<AppUser> userManager)
        {
            // private claims 
            var AuthClaim = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(ClaimTypes.NameIdentifier, user.Id)
            };
            // adding role for more encription
            var UserRoles = await userManager.GetRolesAsync(user);
            foreach (var Role in UserRoles)
            {
                AuthClaim.Add(new Claim(ClaimTypes.Role, Role));
            }
            // private key
            var Key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:Key"]));

            // Token objet 
            var Token = new JwtSecurityToken(
                issuer : _configuration["JWT:Issuer"],
                audience : _configuration["JWT:Audience"],
                expires : DateTime.UtcNow.AddDays(double.Parse(_configuration["JWT:ExpireInDays"]!)),
                claims : AuthClaim,
                signingCredentials : new SigningCredentials(Key,SecurityAlgorithms.HmacSha256Signature)
                );
            return new JwtSecurityTokenHandler().WriteToken(Token);
        }
    }
}
