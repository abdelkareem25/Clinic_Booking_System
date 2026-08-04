namespace Clinic.Api.DTOs.DoctorDto
{
    /// <summary>
    /// The profile fields a doctor request carries, whichever direction it is going.
    ///
    /// Exists so the validation rules can be written once against this shape rather than copied
    /// between the create and update validators - two copies drift, and a field that is optional on
    /// create but mandatory on update is the kind of asymmetry nobody notices until a form that
    /// saved once refuses to save again.
    /// </summary>
    public interface IDoctorProfileFields
    {
        string Name { get; }
        string Specialization { get; }
        string? Phone { get; }
        string? Email { get; }
        decimal? ConsultationFee { get; }
        string? Bio { get; }
    }
}
