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
        public async Task<ActionResult<Pagination<GetDoctorDto>>> GetAll([FromQuery] DoctorSpecParams param)
        {
            var repository = _unitOfWork.Repository<Doctor>();

            // DoctorSpecification applies Skip/Take, so it returns one page...
            var doctors = await repository.GetAllWithSpecAsync(new DoctorSpecification(param));

            // No 404 for an empty page. The collection resource exists; it just has nothing matching
            // right now. 404 means "this resource does not exist", so answering it here forced the
            // client to treat "your search found nothing" as an error and threw away the pagination
            // metadata that tells it how many results there really are.

            // ...and DoctorWithCountSpecification carries the same filters without paging, so it
            // yields the true total. This line used to pass the PAGINATED specification, which
            // clamped Count to PageSize: with 500 doctors and PageSize 5 the API reported 5, the
            // paginator rendered a single page, and 99% of the records were unreachable.
            var totalCount = await repository.CountAsync(new DoctorWithCountSpecification(param));

            var doctorDtos = _mapper.Map<IReadOnlyList<GetDoctorDto>>(doctors);
            return Ok(new Pagination<GetDoctorDto>(param.PageIndex, param.PageSize, totalCount, doctorDtos));
        }
        // GET: api/Doctors{id}
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(DoctorDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<DoctorDto>> GetById(int id)
        {
            var doctor = await _unitOfWork.Repository<Doctor>().GetByIdAsync(id);
            if (doctor == null || doctor.Id == 0) return NotFound("Doctor not found.");
            var doctorDto = _mapper.Map<Doctor, DoctorDto>(doctor);
            return Ok(doctorDto);
        }
        // POST: api/Doctors
        //
        // Creates the doctor AND their published working hours in one atomic write.
        //
        // The alternative the client would otherwise be forced into - POST the doctor, then POST
        // each shift to /api/Schedule - is not atomic: any shift that fails leaves a doctor whose
        // rota is silently half-written, and there is nothing the client can do to roll the earlier
        // calls back. Attaching the shifts to the doctor's navigation collection means EF inserts
        // the parent and the children in a single SaveChanges, which is a single transaction, so the
        // whole rota either lands or none of it does.
        [HttpPost]
        [ProducesResponseType(typeof(GetDoctorDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<GetDoctorDto>> Create(CreateDoctorDto createDoctorDto)
        {
            var doctor = _mapper.Map<CreateDoctorDto, Doctor>(createDoctorDto);

            // DoctorId is left unset on purpose: the parent's key does not exist yet, and EF fills
            // in the foreign key from the relationship once it has been generated.
            foreach (var shift in createDoctorDto.Schedules)
            {
                doctor.DoctorSchedules.Add(new DoctorSchedule
                {
                    DayOfWeek = shift.WeekDay,
                    StartTime = shift.StartTime.ToTimeSpan(),
                    EndTime = shift.EndTime.ToTimeSpan()
                });
            }

            await _unitOfWork.Repository<Doctor>().AddAsync(doctor);
            await _unitOfWork.CompleteAsync(); // commit before mapping so the generated Id is populated

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
            if (existingDoctor == null)
                return NotFound("Doctor not found.");
            _mapper.Map<UpdateDoctorDto, Doctor>(updateDoctorDto, existingDoctor);
            await _unitOfWork.Repository<Doctor>().UpdateAsync(existingDoctor);
            await _unitOfWork.CompleteAsync();
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
            await _unitOfWork.CompleteAsync();
            return NoContent();
        }
    }
}

