using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers
{
    /// <summary>
    /// [Authorize] here is deliberate belt-and-braces alongside the fallback policy registered by
    /// AddClinicAuthorization. The fallback policy only covers endpoints that declare no
    /// authorization metadata at all; this attribute states the requirement explicitly on every
    /// controller deriving from this base, so it survives a change to the global default and is
    /// visible to anyone reading a single controller file.
    ///
    /// Endpoints that must stay public opt out with [AllowAnonymous] on the action.
    /// </summary>
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class APIBaseController : ControllerBase
    {
    }
}
