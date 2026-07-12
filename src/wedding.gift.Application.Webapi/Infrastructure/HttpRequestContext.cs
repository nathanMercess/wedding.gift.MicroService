using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Application.Webapi.Infrastructure;

public sealed class HttpRequestContext(IHttpContextAccessor accessor) : IRequestContext
{
    private HttpContext? HttpContext => accessor.HttpContext;

    public Guid? UserId => ParseGuid(ClaimTypes.NameIdentifier) ?? ParseGuid(JwtRegisteredClaimNames.Sub);
    public Guid? CoupleId => ParseGuid("couple_id");
    public bool IsSuperAdmin => HttpContext?.User.IsInRole(UserRoles.SuperAdmin) == true;
    public string CorrelationId => HttpContext?.TraceIdentifier ?? string.Empty;
    public string RemoteIpAddress => HttpContext?.Connection.RemoteIpAddress?.ToString() ?? string.Empty;

    private Guid? ParseGuid(string claimType)
        => Guid.TryParse(HttpContext?.User.FindFirstValue(claimType), out Guid value) ? value : null;
}
