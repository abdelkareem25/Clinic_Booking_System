using Clinic.Domain.Entites.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Identity
{
    public class ClinicIdentityDbContext : IdentityDbContext<AppUser>
    {
        public ClinicIdentityDbContext(DbContextOptions<ClinicIdentityDbContext> options):base(options)
        {
            
        }
    }
}
