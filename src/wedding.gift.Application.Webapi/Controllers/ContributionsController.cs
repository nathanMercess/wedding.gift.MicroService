using Microsoft.AspNetCore.Mvc;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Extensions;

namespace wedding.gift.Application.Webapi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContributionsController(IContributionService contributionService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ContributionResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ContributionResponseDto>>> GetAll(CancellationToken cancellationToken)
    {
        var contributions = await contributionService.GetAllAsync(cancellationToken);
        return Ok(contributions.Select(x => x.ToResponseDto()));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ContributionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContributionResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var contribution = await contributionService.GetByIdAsync(id, cancellationToken);
        return Ok(contribution.ToResponseDto());
    }

    [HttpPost]
    [ProducesResponseType(typeof(ContributionResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ContributionResponseDto>> Create([FromBody] ContributionCreateDto dto, CancellationToken cancellationToken)
    {
        var created = await contributionService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created.ToResponseDto());
    }
}
