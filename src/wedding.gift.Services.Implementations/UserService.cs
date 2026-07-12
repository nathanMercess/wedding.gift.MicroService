using Microsoft.EntityFrameworkCore;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Crosscutting.Models.DTOs.Auth;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Contracts;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Exceptions;

namespace wedding.gift.Services.Implementations;

public sealed class UserService(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository) : IUserService
{
    public async Task<PagedResult<UserResponseDto>> GetAllAsync(
        UserQueryParams queryParams,
        CancellationToken cancellationToken)
    {
        IQueryable<User> query = userRepository.Query();

        if (!string.IsNullOrWhiteSpace(queryParams.Search))
        {
            string search = queryParams.Search.Trim();
            query = query.Where(x => x.Name.Contains(search) || x.Email.Contains(search));
        }

        int totalCount = await query.CountAsync(cancellationToken);
        List<User> users = await query
            .OrderBy(x => x.Name)
            .Skip((queryParams.Page - 1) * queryParams.PageSize)
            .Take(queryParams.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<UserResponseDto>
        {
            Items = users.Select(ToResponseDto).ToList(),
            TotalCount = totalCount,
            Page = queryParams.Page,
            PageSize = queryParams.PageSize
        };
    }

    public async Task<UserResponseDto> UpdateActiveAsync(
        Guid id,
        Guid actorId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        User user = await userRepository.GetByIdAsync(id, cancellationToken)
                    ?? throw new NotFoundException(ErrorCodes.USER_NOT_FOUND);

        if (id == actorId && !isActive)
            throw new ConflictException(ErrorCodes.CANNOT_DISABLE_CURRENT_USER);

        if (!isActive && user.Role == UserRoles.SuperAdmin)
            await EnsureAnotherActiveSuperAdminAsync(id, cancellationToken);

        if (isActive)
            user.Activate();
        else
        {
            user.Deactivate();
            await refreshTokenRepository.RevokeAllForUserAsync(user.Id, cancellationToken);
        }

        await userRepository.SaveChangesAsync(cancellationToken);
        return ToResponseDto(user);
    }

    public async Task<UserResponseDto> UpdateRoleAsync(
        Guid id,
        Guid actorId,
        string role,
        CancellationToken cancellationToken)
    {
        string normalizedRole = NormalizeRole(role);
        User user = await userRepository.GetByIdAsync(id, cancellationToken)
                    ?? throw new NotFoundException(ErrorCodes.USER_NOT_FOUND);

        if (id == actorId && normalizedRole != UserRoles.SuperAdmin)
            throw new ConflictException(ErrorCodes.CANNOT_DEMOTE_CURRENT_USER);

        if (user.Role == UserRoles.SuperAdmin && normalizedRole != UserRoles.SuperAdmin)
            await EnsureAnotherActiveSuperAdminAsync(id, cancellationToken);

        user.SetRole(normalizedRole);
        await userRepository.SaveChangesAsync(cancellationToken);
        return ToResponseDto(user);
    }

    private async Task EnsureAnotherActiveSuperAdminAsync(Guid id, CancellationToken cancellationToken)
    {
        bool exists = await userRepository.Query()
            .AnyAsync(x => x.Id != id && x.IsActive && x.Role == UserRoles.SuperAdmin, cancellationToken);

        if (!exists)
            throw new ConflictException(ErrorCodes.LAST_SUPER_ADMIN_REQUIRED);
    }

    private static string NormalizeRole(string role)
    {
        if (string.Equals(role, UserRoles.Admin, StringComparison.OrdinalIgnoreCase))
            return UserRoles.Admin;
        if (string.Equals(role, UserRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
            return UserRoles.SuperAdmin;
        if (string.Equals(role, UserRoles.Member, StringComparison.OrdinalIgnoreCase))
            return UserRoles.Member;

        throw new BadRequestException(ErrorCodes.INVALID_USER_ROLE);
    }

    private static UserResponseDto ToResponseDto(User user)
        => new()
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role,
            IsActive = user.IsActive,
            IsEmailConfirmed = user.IsEmailConfirmed,
            CreatedAt = user.CreatedAt
        };
}
