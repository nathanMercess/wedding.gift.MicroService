using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using wedding.gift.Application.Webapi.Controllers.Base;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Extensions;

namespace wedding.gift.Application.Webapi.Controllers;

public sealed class ContributionsController(IContributionService contributionService) : ApiControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<IEnumerable<ContributionResponseDto>> GetAll(CancellationToken cancellationToken)
    {
        IReadOnlyList<Contribution> contributions = await contributionService.GetAllAsync(cancellationToken);
        return contributions.Select(x => x.ToResponseDto());
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<ContributionResponseDto> GetById(Guid id, CancellationToken cancellationToken)
    {
        Contribution contribution = await contributionService.GetByIdAsync(id, cancellationToken);
        return contribution.ToResponseDto();
    }

    [Authorize(Roles = UserRoles.AdminOrSuperAdmin)]
    [HttpPost]
    public async Task<ContributionResponseDto> Create([FromBody] ContributionCreateDto dto, CancellationToken cancellationToken)
    {
        Contribution created = await contributionService.CreateAsync(dto, cancellationToken);
        Response.StatusCode = StatusCodes.Status201Created;
        return created.ToResponseDto();
    }
}
