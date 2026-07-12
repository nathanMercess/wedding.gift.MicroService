using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Crosscutting.Models.DTOs.Auth;

namespace wedding.gift.Services.Contracts;

public interface IUserService
{
    Task<PagedResult<UserResponseDto>> GetAllAsync(UserQueryParams query, CancellationToken cancellationToken);
    Task<UserResponseDto> UpdateActiveAsync(Guid id, Guid actorId, bool isActive, CancellationToken cancellationToken);
    Task<UserResponseDto> UpdateRoleAsync(Guid id, Guid actorId, string role, CancellationToken cancellationToken);
}
