using Clinic.Domain.Entites;
using System.ComponentModel.DataAnnotations;

namespace Clinic.Api.DTOs.ScheduleDto
{
    public class DoctorScheduleDto
    {

        public int Id { get; set; }


        public int DoctorId { get; set; }

        public string DoctorName { get; set; }


        public WeekDay WeekDay { get; set; }


        public TimeOnly StartTime { get; set; }


        public TimeOnly EndTime { get; set; }
    }
}
