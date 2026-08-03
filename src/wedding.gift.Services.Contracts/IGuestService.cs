using wedding.gift.Crosscutting.Models.DTOs;

namespace wedding.gift.Services.Contracts;

public interface IGuestService
{
    Task<IReadOnlyList<GuestSuggestionDto>> GetSuggestionsAsync(string search, CancellationToken cancellationToken);
    Task<GuestConfirmationResponseDto> CreateConfirmationAsync(GuestConfirmationCreateDto dto, CancellationToken cancellationToken);
    Task<GuestSummaryDto> GetSummaryAsync(CancellationToken cancellationToken);
    Task<PagedResult<GuestInvitationResponseDto>> GetInvitationsAsync(GuestInvitationQueryDto query, CancellationToken cancellationToken);
    Task<GuestInvitationResponseDto> GetInvitationByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<GuestInvitationResponseDto> CreateInvitationAsync(GuestInvitationCreateDto dto, CancellationToken cancellationToken);
    Task<GuestInvitationResponseDto> UpdateInvitationAsync(Guid id, GuestInvitationUpdateDto dto, CancellationToken cancellationToken);
    Task<GuestInvitationResponseDto> SetInvitationActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken);
    Task DeleteInvitationAsync(Guid id, CancellationToken cancellationToken);
    Task<GuestInvitationImportResponseDto> ImportInvitationsAsync(Stream csvStream, CancellationToken cancellationToken);
    Task<PagedResult<GuestConfirmationResponseDto>> GetConfirmationsAsync(GuestConfirmationQueryDto query, CancellationToken cancellationToken);
    Task<GuestConfirmationResponseDto> UpdateConfirmationAsync(Guid id, GuestConfirmationCreateDto dto, CancellationToken cancellationToken);
    Task DeleteConfirmationAsync(Guid id, CancellationToken cancellationToken);
    Task<byte[]> ExportConfirmationsCsvAsync(GuestConfirmationQueryDto query, CancellationToken cancellationToken);
}
