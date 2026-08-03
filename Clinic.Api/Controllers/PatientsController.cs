using AutoMapper;
using Clinic.Api.DTOs.PatientDto;
using Clinic.Api.Helper;
using Clinic.Api.Logging;
using Clinic.Domain.Entites;
using Clinic.Domain.Interfaces;
using Clinic.Domain.Interfaces.Specifications.PatientSpec;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers
{

    // Every action here reads or writes protected health information.
    [AuditPhiAccess("Patient")]
    public class PatientsController : APIBaseController
    {
        // private readonly IGenericRepository<Patient> _patientRepository;
        private readonly IMapper _mapper;
        private readonly IUnitOfWork _unitOfWork;

        public PatientsController(//IGenericRepository<Patient> patientRepository,
            IMapper mapper, IUnitOfWork unitOfWork)
        {
            //_patientRepository = patientRepository;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
        }
        // Get All
        [HttpGet]
        public async Task<ActionResult<Pagination<GetPatientDto>>> GetAll([FromQuery] PatientSpecParams param)
        {

            var spec = new PatientSpecification(param);
            var patients = await _unitOfWork.Repository<Patient>().GetAllWithSpecAsync(spec);

            // An empty page is a successful result, not a missing resource - see DoctorsController.
            // This one returned a bare 404 with no body at all, so the client could not even tell
            // whether the search had failed or the endpoint was wrong.

            var countSpec = new PatientCountSpecification(param);
            var totalItems = await _unitOfWork.Repository<Patient>().CountAsync(countSpec);

            var MappedPatients = _mapper.Map<IReadOnlyList<GetPatientDto>>(patients);
            return Ok(new Pagination<GetPatientDto>(param.PageIndex, param.PageSize, totalItems, MappedPatients));
        }
        // Get By Id
        [HttpGet("{id}")]
        public async Task<ActionResult<GetPatientDto>> GetById(int id)
        {
            var patient = await _unitOfWork.Repository<Patient>().GetByIdAsync(id);
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
            await _unitOfWork.Repository<Patient>().AddAsync(patient);
            await _unitOfWork.CompleteAsync(); // commit before mapping so the generated Id is populated
            var mappedPatient = _mapper.Map<GetPatientDto>(patient);
            return Ok(mappedPatient);
        }
        // Put
        [HttpPut("{id}")]
        public async Task<ActionResult<PatientDto>> Update(int id, UpdatePatientDto dto)
        {
            var patient = await _unitOfWork.Repository<Patient>().GetByIdAsync(id);
            if (patient == null) return NotFound();
            _mapper.Map(dto, patient);
            await _unitOfWork.Repository<Patient>().UpdateAsync(patient);
            await _unitOfWork.CompleteAsync();
            return NoContent();
        }
        // Delete
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(int id)
        {
            var patient = await _unitOfWork.Repository<Patient>().GetByIdAsync(id);
            if (patient == null) return NotFound();
            await _unitOfWork.Repository<Patient>().DeleteAsync(patient);
            await _unitOfWork.CompleteAsync();
            return NoContent();
        }

    }
}
