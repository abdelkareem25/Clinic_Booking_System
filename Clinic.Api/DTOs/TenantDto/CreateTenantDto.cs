using System.ComponentModel.DataAnnotations;

namespace Clinic.Api.DTOs.TenantDto
{
    /// <summary>
    /// The request to provision a new clinic.
    ///
    /// Name only. IsActive is not accepted because a tenant being created is by definition being
    /// created active, and there is no screen that would ask otherwise; Id is not accepted because
    /// the database assigns it. Both would be mass-assignment surface for no benefit.
    /// </summary>
    public class CreateTenantDto
    {
        [Required]
        public string Name { get; set; } = null!;
    }
}
