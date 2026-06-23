using AutoMapper;
using Clinic.Api.DTOs.AppointmentDto;
using Clinic.Api.Helper;
using Clinic.Domain.Entites;
using Clinic.Domain.Interfaces;
using Clinic.Domain.Interfaces.Repository;
using Clinic.Domain.Interfaces.Specifications.AppointmentSpec;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers
{

    public class AppointmentsController : APIBaseController
    {
        private readonly IGenericRepository<Doctor> _doctorRepository;
        private readonly IGenericRepository<Patient> _patientRepository;
        private readonly IMapper _mapper;
        private readonly IAppointmentRepository _appointmentRepository;

        public AppointmentsController(
             IGenericRepository<Doctor> doctorRepository
            , IGenericRepository<Patient> patientRepository
            , IMapper mapper
            , IAppointmentRepository appointmentRepository)
        {
            _appointmentRepository = appointmentRepository;
            _doctorRepository = doctorRepository;
            _patientRepository = patientRepository;
            _mapper = mapper;
            _appointmentRepository = appointmentRepository;
        }

        [HttpPost]
        public async Task<ActionResult> Create(CreateAppointmentDto dto)
        {

            var doctor = await _doctorRepository.GetByIdAsync(dto.DoctorId);
            if (doctor == null)
                return NotFound($"Doctor with id {dto.DoctorId} not found.");
            var patient = await _patientRepository.GetByIdAsync(dto.PatientId);
            if (patient == null)
                return NotFound($"Patient with id {dto.PatientId} not found.");
            var isDoctorAvailable = await _appointmentRepository.IsDoctorAvailableAsync(dto.DoctorId, dto.AppointmentDate, null);
            if (!isDoctorAvailable)
                return BadRequest($"Doctor with id {dto.DoctorId} is not available for the selected date and time.");

            var mappedAppointment = _mapper.Map<Appointment>(dto); // Map the DTO to the Appointment entity
            await _appointmentRepository.AddAsync(mappedAppointment);

            var result = _mapper.Map<AppointmentDto>(mappedAppointment); // Map the saved Appointment entity to the AppointmentDto
            return Ok(result);
        }

        // Delete an appointment by id
        [Authorize(Roles = "Admin,Doctor")]
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(int id)
        {
            var appointment = await _appointmentRepository.GetByIdAsync(id);
            if (appointment == null)
                return NotFound($"Appointment with id {id} not found.");
            await _appointmentRepository.DeleteAsync(appointment);
            return NoContent();
        }
        // Update an existing appointment
        [Authorize(Roles = "Admin,Doctor")]
        [HttpPut("{id}")]
        public async Task<ActionResult> Update(int id, UpdateAppointmentDto dto)
        {
            var appointment = await _appointmentRepository.GetByIdAsync(id);
            if (appointment == null)
                return NotFound($"Appointment with id {id} not found.");
            var doctor = await _doctorRepository.GetByIdAsync(dto.DoctorId);
            if (doctor == null)
                return NotFound($"Doctor with id {dto.DoctorId} not found.");
            var patient = await _patientRepository.GetByIdAsync(dto.PatientId);
            if (patient == null)
                return NotFound($"Patient with id {dto.PatientId} not found.");
            var isDoctorAvailable = await _appointmentRepository.IsDoctorAvailableAsync(dto.DoctorId, dto.AppointmentDate, id);
            if (!isDoctorAvailable)
                return BadRequest($"Doctor with id {dto.DoctorId} is not available for the selected date and time.");
            _mapper.Map(dto, appointment);
            await _appointmentRepository.UpdateAsync(appointment);
            var result = _mapper.Map<AppointmentDto>(appointment);
            return Ok(result);
        }
        #region GetTypes
        // Get all appointments
        [HttpGet]
        public async Task<ActionResult<Pagination<AppointmentDto>>> GetAllAppointments(AppointmentSpecParams param)
        {   
            var spec = new AppointmentSpecification(param);
            var appointments = await _appointmentRepository.GetAllWithSpecAsync(spec);
            if (appointments == null || !appointments.Any())
                return NotFound("No appointments found.");
            var TotalCounts = new AppointmentWithCountSpecification(param);
            var count = await _appointmentRepository.CountAsync(spec);
            var result = _mapper.Map<IReadOnlyList<AppointmentDto>>(appointments);

            return Ok(new Pagination<AppointmentDto>(param.PageIndex,param.PageSize,count,result));
        }
        // Get an appointment by id
        [HttpGet("{id}")]
        public async Task<ActionResult<AppointmentDto >> GetById(int id)
        {
            var spec = new AppointmentWithDoctorAndPatientSpec(id);
            var appointment = await _appointmentRepository.GetEntityWithSpec(spec);
            if (appointment == null)
                return NotFound($"Appointment with id {id} not found.");
            var result = _mapper.Map<Appointment, AppointmentDto>(appointment);
            return Ok(result);
        }
        // Get an appointment by DoctorName
        [HttpGet("doctor/{doctorName}")]
        public async Task<ActionResult<IReadOnlyList<AppointmentDto>>> GetByDoctorName(string doctorName)
        {
            var spec = new AppointmentWithDoctorNameSpec(doctorName);
            var appointment = await _appointmentRepository.ListAsync(spec);
            var result = _mapper.Map<IReadOnlyList<Appointment>, IReadOnlyList<AppointmentDto>>(appointment);
            return Ok(result);
        }
        // Get an appointment by PatientName
        [HttpGet("patient{patientName}")]
        public async Task<ActionResult<IReadOnlyList<AppointmentDto>>> GetByPatientName(string patientName)
        {
            var spec = new AppointmentWithPatientNameSpec(patientName);
            var appointment = await _appointmentRepository.ListAsync(spec);
            if (!appointment.Any()) return NotFound($"No appointments found for patient '{patientName}'.");
            var result = _mapper.Map<AppointmentDto>(appointment);
            return Ok(result);
        }
        #endregion

    }
}
