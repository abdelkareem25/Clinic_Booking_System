using AutoMapper;
using Clinic.Api.DTOs.ScheduleDto;
using Clinic.Api.Helper;
using Clinic.Domain.Entites;
using Clinic.Domain.Interfaces;
using Clinic.Domain.Interfaces.Specifications.ScheduleSpec;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers
{
    public class ScheduleController : APIBaseController
    {
       
   
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public ScheduleController(IUnitOfWork unitOfWork
            , IMapper mapper)
        {
            
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }
        // Getall
        [HttpGet]
        public async Task<ActionResult<Pagination<DoctorScheduleDto>>> GetSchedules([FromQuery]DoctorScheduleSpecParams param)
        {
            var spec = new ScheduleSpecification(param);
            var Schedule = await _unitOfWork.Repository<DoctorSchedule>().GetAllWithSpecAsync(spec);
            var total = new ScheduleCountSpecification(param);
            var count = await _unitOfWork.Repository<DoctorSchedule>().CountAsync(spec);
            var data = _mapper.Map<IReadOnlyList<DoctorScheduleDto>>(spec);
            return Ok(new Pagination<DoctorScheduleDto>(param.PageIndex,param.PageSize, count, data));
        }
        //GetById
        [HttpGet("{id}")]
        public async Task<ActionResult<DoctorScheduleDto>> GetSchedule(int id)
        {
           var schedule = await _unitOfWork.Repository<DoctorSchedule>().GetByIdAsync(id);
            if (schedule == null) return BadRequest();
            return Ok(_mapper.Map<DoctorScheduleDto>(schedule));
        }
        // Create
        [HttpPost]
        public async Task<ActionResult<DoctorScheduleDto>> Create(CreateDoctorScheduleDto dto)
        {
            var schedule = _mapper.Map<DoctorSchedule>(dto);
            await _unitOfWork.Repository<DoctorSchedule>().AddAsync(schedule);
            var result = _mapper.Map<DoctorScheduleDto>(schedule);
            return CreatedAtAction(
            nameof(GetSchedule), new { id = schedule.Id }, result);
        }
        // Update
        [HttpPut("{id}")]
        public async Task<ActionResult> Update(int id,UpdateDoctorScheduleDto dto)
        {
            var schedule = await _unitOfWork.Repository<DoctorSchedule>().GetByIdAsync(id);

            if (schedule is null)
                return NotFound();

            _mapper.Map(dto, schedule);

            await _unitOfWork.Repository<DoctorSchedule>().UpdateAsync(schedule);

            return NoContent();
        }
        // Delete
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSchedule(int id)
        {
            var schedule = await _unitOfWork.Repository<DoctorSchedule>().GetByIdAsync(id);

            if (schedule is null)
                return NotFound();
            await _unitOfWork.Repository<DoctorSchedule>().DeleteAsync(schedule);
            return NoContent();
        }

    }
}
