using FluentValidation;

namespace Clinic.Api.DTOs.PatientDto.Validators
{
    /// <summary>
    /// This class existed as an empty shell with no base type - so it was not a validator at all,
    /// merely a file that looked like one.
    ///
    /// It matters more here than elsewhere: UpdatePatientDto carries no DataAnnotations either, so
    /// until now a PUT could blank a patient's name and phone number, or set a date of birth in the
    /// future, and the API would accept all of it.
    ///
    /// The rules mirror CreatePatientValidators - an update must not be able to put a record into a
    /// state a create would have rejected.
    /// </summary>
    public class UpdatePatientValidators : AbstractValidator<UpdatePatientDto>
    {
        public UpdatePatientValidators()
        {
            RuleFor(p => p.Name)
                .NotEmpty().WithMessage("Name is required.")
                .MaximumLength(100).WithMessage("Name cannot exceed 100 characters.");

            RuleFor(p => p.Phone)
                .NotEmpty().WithMessage("Phone is required.")
                .MaximumLength(15).WithMessage("Phone cannot exceed 15 characters.");

            RuleFor(p => p.Gender)
                .NotEmpty().WithMessage("Gender is required.");

            RuleFor(p => p.DateOfBirth)
                .NotEmpty().WithMessage("Date of birth is required.")
                .LessThan(_ => DateTime.UtcNow).WithMessage("Date of birth must be in the past.");
        }
    }
}
