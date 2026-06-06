using Clinic.Domain.Entites.Identity;
using Microsoft.AspNetCore.Identity;

namespace Clinic.Infrastructure.Identity
{
    public static class ClinicIdentityDbContextSeed
    {
        public static async Task SeedUserAsync(UserManager<AppUser> userManager)
        {
            if (!userManager.Users.Any())
            {
                var User = new AppUser
                {
                    DisplayName = "Abdelkarim Badr",
                    Email = "AbdelkarimBadr@gmail.com",
                    UserName = "AbdelkarimBadr",
                    PhoneNumber = "01000000000",
                };
                await userManager.CreateAsync(User, "Password123!");
            }
            ;
        }
    }
}
