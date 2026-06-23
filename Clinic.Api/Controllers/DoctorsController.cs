using AutoMapper;
using Clinic.Api.DTOs.DoctorDto;
using Clinic.Domain.Entites;
using Clinic.Domain.Interfaces.Repository;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers
{
    public class DoctorsController : APIBaseController
    {
        private readonly IGenericRepository<Doctor> _doctorRepository;
        private readonly IMapper _mapper;

        public DoctorsController(IGenericRepository<Doctor> doctorRepository
            , IMapper mapper)
        {
            _doctorRepository = doctorRepository;
            _mapper = mapper;
        }

        // GET: api/Doctors
        [HttpGet]
        [ProducesResponseType(typeof(GetDoctorDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<IReadOnlyList<GetDoctorDto>>> GetAll()
        {

            var doctors = await _doctorRepository.GetAllAsync();
            if (doctors == null || doctors.Count == 0)
                return NotFound("No doctors found.");
            var doctorDtos = _mapper.Map<IReadOnlyList<GetDoctorDto>>(doctors);
            return Ok(doctorDtos);
        }
        // GET: api/Doctors{id}
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(DoctorDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<DoctorDto>> GetById(int id)
        {
            var doctor = await _doctorRepository.GetByIdAsync(id);
            if(doctor == null || doctor.Id == 0) return NotFound("Doctor not found.");
            var doctorDto = _mapper.Map<Doctor, DoctorDto>(doctor);
            return Ok(doctorDto);
        }
        // POST: api/Doctors
        [HttpPost]
        [ProducesResponseType(typeof(GetDoctorDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<GetDoctorDto>> Create(CreateDoctorDto createDoctorDto)
        {
            var doctor = _mapper.Map<CreateDoctorDto, Doctor>(createDoctorDto);
            await _doctorRepository.AddAsync(doctor);
            var doctorDto = _mapper.Map<Doctor, GetDoctorDto>(doctor);
            return CreatedAtAction(nameof(GetById), new { id = doctor.Id }, doctorDto);
        }
        // PUT: api/Doctors{id}
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> Update(int id, UpdateDoctorDto updateDoctorDto)
        {
            var existingDoctor = await _doctorRepository.GetByIdAsync(id);
            if (existingDoctor == null )
                return NotFound("Doctor not found.");
            _mapper.Map<UpdateDoctorDto, Doctor>(updateDoctorDto ,existingDoctor);
            await _doctorRepository.UpdateAsync(existingDoctor);
            var updatedDto = _mapper.Map<GetDoctorDto>(existingDoctor);
            return Ok(updatedDto);

        }
        // DELETE: api/Doctors{id}
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> Delete(int id)
        {
            var exsistingDoctor = await _doctorRepository.GetByIdAsync(id);
            if (exsistingDoctor == null)
                return NotFound();
            await _doctorRepository.DeleteAsync(exsistingDoctor);
            return NoContent();
        }
    }
}

