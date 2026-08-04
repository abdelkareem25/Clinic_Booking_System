using FluentValidation;

namespace Clinic.Api.DTOs.DoctorDto.Validators
{
    public class CreateDoctorDtoValidator : DoctorProfileValidator<CreateDoctorDto>
    {
        public CreateDoctorDtoValidator()
        {
            RuleForEach(x => x.Schedules).SetValidator(new DoctorShiftDtoValidator());

            // Set-level rules: these are properties of the rota as a whole, not of one shift. The
            // dialog enforces both before it will submit, but the dialog is not the only caller.
            RuleFor(x => x.Schedules)
                .Must(HaveNoDuplicateShifts)
                .WithMessage("The same shift has been added twice.")
                .Must(HaveNoOverlappingShifts)
                .WithMessage("Two shifts on the same day overlap.");
        }

        private static bool HaveNoDuplicateShifts(List<DoctorShiftDto> shifts) =>
            shifts is null || shifts
                .Select(shift => (shift.WeekDay, shift.StartTime, shift.EndTime))
                .Distinct()
                .Count() == shifts.Count;

        /// <summary>
        /// Half-open intervals: a shift ending at 13:00 and the next starting at 13:00 are adjacent,
        /// not overlapping, so the comparison is strict. Treating touching shifts as an overlap
        /// would reject the ordinary "morning clinic, then afternoon clinic" rota.
        /// </summary>
        private static bool HaveNoOverlappingShifts(List<DoctorShiftDto> shifts)
        {
            if (shifts is null)
            {
                return true;
            }

            foreach (var sameDay in shifts.GroupBy(shift => shift.WeekDay))
            {
                var ordered = sameDay.OrderBy(shift => shift.StartTime).ToList();

                for (var i = 1; i < ordered.Count; i++)
                {
                    if (ordered[i].StartTime < ordered[i - 1].EndTime)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
