using AutoMapper;
using Clinic.Api.DTOs.DoctorDto;
using Clinic.Api.Helper;
using Clinic.Domain.Entites;
using Clinic.Domain.Interfaces;
using Clinic.Domain.Interfaces.Specifications.DoctorSpec;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers
{
    public class DoctorsController : APIBaseController
    {
       // private readonly IGenericRepository<Doctor> _doctorRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public DoctorsController(//IGenericRepository<Doctor> doctorRepository
            IUnitOfWork unitOfWork
            , IMapper mapper)
        {
            //_doctorRepository = doctorRepository;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        // GET: api/Doctors
        [HttpGet]
        [ProducesResponseType(typeof(GetDoctorDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<Pagination<GetDoctorDto>>> GetAll([FromQuery]DoctorSpecParams param)
        {
            var spec = new DoctorSpecification(param);
            var doctors = await _unitOfWork.Repository<Doctor>().GetAllWithSpecAsync(spec);
            if (doctors == null || doctors.Count == 0)
                return NotFound("No doctors found.");
            var count = new DoctorWithCountSpecification(param);
            var totalCount = await _unitOfWork.Repository<Doctor>().CountAsync(spec);
            var doctorDtos = _mapper.Map<IReadOnlyList<GetDoctorDto>>(doctors);
            return Ok(new Pagination<GetDoctorDto>(param.PageIndex,param.PageSize,totalCount,doctorDtos));
        }
        // GET: api/Doctors{id}
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(DoctorDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<DoctorDto>> GetById(int id)
        {
            var doctor = await _unitOfWork.Repository<Doctor>().GetByIdAsync(id);
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
            await _unitOfWork.Repository<Doctor>().AddAsync(doctor);
            var doctorDto = _mapper.Map<Doctor, GetDoctorDto>(doctor);
            return CreatedAtAction(nameof(GetById), new { id = doctor.Id }, doctorDto);
        }
        // PUT: api/Doctors{id}
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> Update(int id, UpdateDoctorDto updateDoctorDto)
        {
            var existingDoctor = await _unitOfWork.Repository<Doctor>().GetByIdAsync(id);
            if (existingDoctor == null )
                return NotFound("Doctor not found.");
            _mapper.Map<UpdateDoctorDto, Doctor>(updateDoctorDto ,existingDoctor);
            await _unitOfWork.Repository<Doctor>().UpdateAsync(existingDoctor);
            var updatedDto = _mapper.Map<GetDoctorDto>(existingDoctor);
            return Ok(updatedDto);

        }
        // DELETE: api/Doctors{id}
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> Delete(int id)
        {
            var exsistingDoctor = await _unitOfWork.Repository<Doctor>().GetByIdAsync(id);
            if (exsistingDoctor == null)
                return NotFound();
            await _unitOfWork.Repository<Doctor>().DeleteAsync(exsistingDoctor);
            return NoContent();
        }
    }
}

