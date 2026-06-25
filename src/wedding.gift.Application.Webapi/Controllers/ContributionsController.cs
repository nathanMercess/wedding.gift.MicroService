using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using wedding.gift.Application.Webapi.Controllers.Base;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Application.Webapi.Controllers;

public sealed class ContributionsController(IContributionService contributionService) : ApiControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<IEnumerable<ContributionResponseDto>> GetAll(CancellationToken cancellationToken)
        => await contributionService.GetAllAsync(cancellationToken);

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<ContributionResponseDto> GetById(Guid id, CancellationToken cancellationToken)
        => await contributionService.GetByIdAsync(id, cancellationToken);

    [Authorize(Roles = UserRoles.AdminOrSuperAdmin)]
    [HttpPost]
    public async Task<ContributionResponseDto> Create([FromBody] ContributionCreateDto dto, CancellationToken cancellationToken)
    {
        ContributionResponseDto created = await contributionService.CreateAsync(dto, cancellationToken);
        Response.StatusCode = StatusCodes.Status201Created;
        return created;
    }
}
