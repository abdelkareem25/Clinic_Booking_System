using AutoMapper;
using Clinic.Api.DTOs.TenantDto;
using Clinic.Domain.Entites;
using Clinic.Domain.Entites.Identity;
using Clinic.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers
{
    /// <summary>
    /// Provisioning clinics. Deliberately the smallest surface that makes a tenant usable: create
    /// one, and read one back.
    ///
    /// There is no list, no update and no delete, and their absence is a decision rather than an
    /// omission:
    ///
    ///   - A LIST would enumerate every clinic using the system to any administrator of any one of
    ///     them. That is the one genuinely sensitive thing this table holds.
    ///   - DELETE is refused by the database anyway - the tenant foreign keys are Restrict, so
    ///     removing a clinic that has ever held a record cannot succeed. An endpoint that always
    ///     fails is worse than no endpoint.
    ///   - UPDATE (renaming, deactivating) has no screen asking for it yet, and deactivation in
    ///     particular is not currently enforced anywhere - see Tenant.IsActive. Shipping it would
    ///     imply a guarantee that does not exist.
    ///
    /// Tenant itself is global: it carries no TenantId and no query filter, because a row cannot be
    /// filtered by itself. Access is therefore controlled here, by authorization, rather than by
    /// the isolation that protects everything else.
    /// </summary>
    public class TenantsController : APIBaseController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public TenantsController(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        // GET: api/Tenants/{id}
        //
        // Exists primarily so Create has somewhere to point its Location header, which is what
        // makes a 201 a useful answer rather than a bare acknowledgement.
        [Authorize(Roles = ClinicRoles.Admin)]
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(TenantDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<TenantDto>> GetById(int id)
        {
            var tenant = await _unitOfWork.Repository<Tenant>().GetByIdAsync(id);

            if (tenant is null) return NotFound("Tenant not found.");

            return Ok(_mapper.Map<TenantDto>(tenant));
        }

        // POST: api/Tenants
        [Authorize(Roles = ClinicRoles.Admin)]
        [HttpPost]
        [ProducesResponseType(typeof(TenantDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<TenantDto>> Create(CreateTenantDto dto)
        {
            var tenant = _mapper.Map<Tenant>(dto);

            await _unitOfWork.Repository<Tenant>().AddAsync(tenant);

            // Commit before mapping so the generated Id and the stamped CreatedAtUtc are populated
            // - the same reason the other controllers do it in this order.
            await _unitOfWork.CompleteAsync();

            return CreatedAtAction(nameof(GetById), new { id = tenant.Id }, _mapper.Map<TenantDto>(tenant));
        }
    }
}
