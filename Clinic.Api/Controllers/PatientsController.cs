using AutoMapper;
using Clinic.Api.DTOs.PatientDto;
using Clinic.Domain.Entites;
using Clinic.Domain.Interfaces.Repository;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers
{

    public class PatientsController : APIBaseController
    {
        private readonly IGenericRepository<Patient> _patientRepository;
        private readonly IMapper _mapper;

        public PatientsController(IGenericRepository<Patient> patientRepository , 
            IMapper mapper)
        {
            _patientRepository = patientRepository;
            _mapper = mapper;
        }   
        // Get All
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<GetPatientDto>>> GetAll()
        {
            var patients = await _patientRepository.GetAllAsync();
            if (patients == null || patients.Count == 0)
            {
                return NotFound();
            }
            var MappedPatients = _mapper.Map<IReadOnlyList<GetPatientDto>>(patients);
            return Ok(MappedPatients);
        }
        // Get By Id
        [HttpGet("{id}")]
        public async Task<ActionResult<GetPatientDto>> GetById(int id)
        {
            var patient = await _patientRepository.GetByIdAsync(id);
            if (patient == null)
            {
                return NotFound();
            }
            var mappedPatient = _mapper.Map<GetPatientDto>(patient);
            return Ok(mappedPatient);
        }
        // Post 
        [HttpPost]
        public async Task<ActionResult<PatientDto>> Create(CreatePatientDto dto)
        {
            var patient = _mapper.Map<CreatePatientDto, Patient>(dto);
            await _patientRepository.AddAsync(patient);
            var mappedPatient = _mapper.Map<GetPatientDto>(patient);
            return Ok(mappedPatient);
        }
        // Put
        [HttpPut("{id}")]
        public async Task<ActionResult<PatientDto>> Update(int id , UpdatePatientDto dto)
        {
            var patient = await _patientRepository.GetByIdAsync(id);
            if(patient == null) return NotFound();
            _mapper.Map(dto, patient);
            await _patientRepository.UpdateAsync(patient);
            return NoContent();
        }
        // Delete
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(int id)
        {
            var patient = await _patientRepository.GetByIdAsync(id);
            if(patient == null) return NotFound();
            await _patientRepository.DeleteAsync(patient);
            return NoContent();
        }

    }
}
