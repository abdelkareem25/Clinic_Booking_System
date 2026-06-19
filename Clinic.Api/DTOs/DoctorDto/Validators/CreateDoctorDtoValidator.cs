using FluentValidation;

namespace Clinic.Api.DTOs.DoctorDto.Validators
{
    public class CreateDoctorDtoValidator : AbstractValidator<CreateDoctorDto>
    {
        public CreateDoctorDtoValidator()
        {
                RuleFor(n=>n.Name).NotEmpty().MaximumLength(100);
                RuleFor(s => s.Specialization).NotEmpty();
                
        }
    }
}

