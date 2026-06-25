using wedding.gift.Crosscutting.Models.DTOs.Auth;
using wedding.gift.Domain.Model.Entities;

namespace wedding.gift.Services.Implementations.Extensions;

public static class UserDtoMappingExtensions
{
    public static RegisterResponseDto ToRegisterResponseDto(this User user)
        => new()
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email
        };

    public static LoginResponseDto ToLoginResponseDto(this User user, string accessToken, DateTime expiresAtUtc)
        => new()
        {
            AccessToken = accessToken,
            ExpiresAtUtc = expiresAtUtc,
            UserName = user.Name,
            Email = user.Email,
            Role = user.Role
        };
}
