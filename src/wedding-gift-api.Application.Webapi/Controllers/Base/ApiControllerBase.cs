using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace wedding-gift-api.Application.Webapi.Controllers.Base;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
}