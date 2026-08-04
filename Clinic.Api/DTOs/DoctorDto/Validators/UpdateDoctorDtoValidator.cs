namespace Clinic.Api.DTOs.DoctorDto.Validators
{
    /// <summary>
    /// Update carries exactly the shared profile rules and nothing else - the rota is not editable
    /// through this endpoint. See <see cref="DoctorProfileValidator{T}"/>.
    /// </summary>
    public class UpdateDoctorDtoValidator : DoctorProfileValidator<UpdateDoctorDto>
    {
    }
}
