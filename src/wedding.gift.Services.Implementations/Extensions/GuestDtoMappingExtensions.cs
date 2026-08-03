using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;

namespace wedding.gift.Services.Implementations.Extensions;

public static class GuestDtoMappingExtensions
{
    public static GuestInvitationResponseDto ToResponseDto(this GuestInvitation entity, ConfirmedGuest? confirmedGuest)
        => new()
        {
            Id = entity.Id,
            Name = entity.Name,
            IsActive = entity.IsActive,
            IsConfirmed = confirmedGuest is not null,
            Status = confirmedGuest is not null
                ? GuestInvitationStatuses.Confirmed
                : entity.IsActive ? GuestInvitationStatuses.Pending : GuestInvitationStatuses.Inactive,
            SubmittedByName = confirmedGuest?.GuestConfirmation.Guests.FirstOrDefault(x => x.IsSubmitter)?.Name,
            PartySize = confirmedGuest?.GuestConfirmation.Guests.Count,
            ConfirmedAtUtc = confirmedGuest?.GuestConfirmation.ConfirmedAtUtc,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc
        };

    public static GuestConfirmationResponseDto ToResponseDto(this GuestConfirmation entity)
    {
        ConfirmedGuest submitter = entity.Guests.First(x => x.IsSubmitter);

        return new GuestConfirmationResponseDto
        {
            Id = entity.Id,
            SubmittedByName = submitter.Name,
            PartySize = entity.Guests.Count,
            ConfirmedAtUtc = DateTime.SpecifyKind(entity.ConfirmedAtUtc, DateTimeKind.Utc),
            UpdatedAtUtc = DateTime.SpecifyKind(entity.UpdatedAtUtc, DateTimeKind.Utc),
            Guests = entity.Guests
                .OrderByDescending(x => x.IsSubmitter)
                .ThenBy(x => x.CreatedAtUtc)
                .Select(x => new ConfirmedGuestResponseDto
                {
                    Id = x.Id,
                    GuestInvitationId = x.GuestInvitationId,
                    Name = x.Name,
                    Source = x.Source,
                    IsSubmitter = x.IsSubmitter,
                    CreatedAtUtc = DateTime.SpecifyKind(x.CreatedAtUtc, DateTimeKind.Utc)
                })
                .ToList()
        };
    }
}
