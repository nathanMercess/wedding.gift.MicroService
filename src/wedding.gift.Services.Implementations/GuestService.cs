using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Contracts;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Exceptions;
using wedding.gift.Services.Implementations.Extensions;

namespace wedding.gift.Services.Implementations;

public sealed class GuestService(
    IGuestRepository guestRepository,
    IRequestContext? requestContext = null,
    IOperationalRepository? operationalRepository = null) : IGuestService
{
    public async Task<IReadOnlyList<GuestSuggestionDto>> GetSuggestionsAsync(string search, CancellationToken cancellationToken)
    {
        string normalizedSearch = GuestNameNormalizer.Normalize(search ?? string.Empty);

        if (normalizedSearch.Length < 3)
            throw new BadRequestException(ErrorCodes.INVALID_GUEST_CONFIRMATION);

        IQueryable<Guid> confirmedIds = guestRepository.QueryConfirmedGuests()
            .Where(x => x.GuestConfirmation.CoupleId == Couple.SingletonId && x.GuestInvitationId.HasValue)
            .Select(x => x.GuestInvitationId!.Value);

        return await guestRepository.QueryInvitations()
            .Where(x => x.CoupleId == Couple.SingletonId && x.IsActive && x.NormalizedName.Contains(normalizedSearch) && !confirmedIds.Contains(x.Id))
            .OrderByDescending(x => x.NormalizedName.StartsWith(normalizedSearch))
            .ThenBy(x => x.Name)
            .Take(8)
            .Select(x => new GuestSuggestionDto { Id = x.Id, Name = x.Name })
            .ToListAsync(cancellationToken);
    }

    public async Task<GuestConfirmationResponseDto> CreateConfirmationAsync(GuestConfirmationCreateDto dto, CancellationToken cancellationToken)
    {
        GuestConfirmation confirmation = GuestConfirmation.Create(Couple.SingletonId);
        List<ConfirmedGuest> guests = await PrepareGuestsAsync(
            confirmation.Id,
            confirmation.CoupleId,
            dto.Guests,
            null,
            cancellationToken);

        foreach (ConfirmedGuest guest in guests)
            confirmation.AddGuest(guest);

        await guestRepository.AddConfirmationAsync(confirmation, cancellationToken);
        await SaveAsync(ErrorCodes.GUEST_INVITATION_ALREADY_CONFIRMED, cancellationToken);
        return await GetConfirmationResponseAsync(confirmation.Id, cancellationToken);
    }

    public async Task<GuestSummaryDto> GetSummaryAsync(CancellationToken cancellationToken)
    {
        Guid coupleId = GetCoupleId();
        IQueryable<GuestInvitation> invitations = guestRepository.QueryInvitations().Where(x => x.CoupleId == coupleId);
        IQueryable<ConfirmedGuest> guests = guestRepository.QueryConfirmedGuests().Where(x => x.GuestConfirmation.CoupleId == coupleId);

        return new GuestSummaryDto
        {
            InvitationCount = await invitations.CountAsync(cancellationToken),
            PendingInvitationCount = await invitations.CountAsync(x => x.IsActive && !guests.Any(g => g.GuestInvitationId == x.Id), cancellationToken),
            ConfirmationCount = await guestRepository.QueryConfirmations().CountAsync(x => x.CoupleId == coupleId, cancellationToken),
            ConfirmedGuestCount = await guests.CountAsync(cancellationToken),
            FreeTextGuestCount = await guests.CountAsync(x => x.Source == GuestConfirmationSources.FreeText, cancellationToken)
        };
    }

    public async Task<PagedResult<GuestInvitationResponseDto>> GetInvitationsAsync(GuestInvitationQueryDto queryDto, CancellationToken cancellationToken)
    {
        ValidatePagination(queryDto.Page, queryDto.PageSize);
        Guid coupleId = GetCoupleId();
        IQueryable<ConfirmedGuest> confirmedGuests = guestRepository.QueryConfirmedGuests();
        IQueryable<GuestInvitation> query = guestRepository.QueryInvitations().Where(x => x.CoupleId == coupleId);

        if (!string.IsNullOrWhiteSpace(queryDto.Search))
        {
            string search = GuestNameNormalizer.Normalize(queryDto.Search);
            query = query.Where(x => x.NormalizedName.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(queryDto.Status))
        {
            if (!GuestInvitationStatuses.Allowed.Contains(queryDto.Status))
                throw new BadRequestException(ErrorCodes.VALIDATION_ERROR);

            if (string.Equals(queryDto.Status, GuestInvitationStatuses.Active, StringComparison.OrdinalIgnoreCase))
                query = query.Where(x => x.IsActive);

            if (string.Equals(queryDto.Status, GuestInvitationStatuses.Inactive, StringComparison.OrdinalIgnoreCase))
                query = query.Where(x => !x.IsActive);

            if (string.Equals(queryDto.Status, GuestInvitationStatuses.Confirmed, StringComparison.OrdinalIgnoreCase))
                query = query.Where(x => confirmedGuests.Any(g => g.GuestInvitationId == x.Id));

            if (string.Equals(queryDto.Status, GuestInvitationStatuses.Pending, StringComparison.OrdinalIgnoreCase))
                query = query.Where(x => x.IsActive && !confirmedGuests.Any(g => g.GuestInvitationId == x.Id));
        }

        int totalCount = await query.CountAsync(cancellationToken);
        List<GuestInvitation> invitations = await query.OrderBy(x => x.Name)
            .Skip((queryDto.Page - 1) * queryDto.PageSize)
            .Take(queryDto.PageSize)
            .ToListAsync(cancellationToken);
        Guid[] invitationIds = invitations.Select(x => x.Id).ToArray();
        Dictionary<Guid, ConfirmedGuest> confirmationsByInvitation = (await confirmedGuests
                .Where(x => x.GuestInvitationId.HasValue && invitationIds.Contains(x.GuestInvitationId.Value))
                .ToListAsync(cancellationToken))
            .ToDictionary(x => x.GuestInvitationId!.Value);

        return new PagedResult<GuestInvitationResponseDto>
        {
            Items = invitations.Select(x => x.ToResponseDto(confirmationsByInvitation.GetValueOrDefault(x.Id))).ToList(),
            TotalCount = totalCount,
            Page = queryDto.Page,
            PageSize = queryDto.PageSize
        };
    }

    public async Task<GuestInvitationResponseDto> CreateInvitationAsync(GuestInvitationCreateDto dto, CancellationToken cancellationToken)
    {
        Guid coupleId = GetCoupleId();
        string name = CleanName(dto.Name);
        string normalizedName = GuestNameNormalizer.Normalize(name);
        await EnsureInvitationNameAvailableAsync(coupleId, normalizedName, null, cancellationToken);
        GuestInvitation invitation = GuestInvitation.Create(coupleId, name, normalizedName);

        await guestRepository.AddInvitationAsync(invitation, cancellationToken);
        await AddAuditAsync("GuestInvitationCreated", "GuestInvitation", invitation.Id, coupleId, cancellationToken);
        await SaveAsync(ErrorCodes.GUEST_INVITATION_ALREADY_EXISTS, cancellationToken);
        return invitation.ToResponseDto(null);
    }

    public async Task<GuestInvitationResponseDto> GetInvitationByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        Guid coupleId = GetCoupleId();
        GuestInvitation invitation = await GetInvitationAsync(id, coupleId, cancellationToken);
        ConfirmedGuest? confirmedGuest = await guestRepository.QueryConfirmedGuests()
            .FirstOrDefaultAsync(x => x.GuestInvitationId == id, cancellationToken);
        return invitation.ToResponseDto(confirmedGuest);
    }

    public async Task<GuestInvitationResponseDto> UpdateInvitationAsync(Guid id, GuestInvitationUpdateDto dto, CancellationToken cancellationToken)
    {
        Guid coupleId = GetCoupleId();
        GuestInvitation invitation = await GetInvitationAsync(id, coupleId, cancellationToken);
        string name = CleanName(dto.Name);
        string normalizedName = GuestNameNormalizer.Normalize(name);
        await EnsureInvitationNameAvailableAsync(coupleId, normalizedName, id, cancellationToken);

        invitation.Update(name, normalizedName);
        await AddAuditAsync("GuestInvitationUpdated", "GuestInvitation", id, coupleId, cancellationToken);
        await SaveAsync(ErrorCodes.GUEST_INVITATION_ALREADY_EXISTS, cancellationToken);
        ConfirmedGuest? confirmedGuest = await guestRepository.QueryConfirmedGuests()
            .FirstOrDefaultAsync(x => x.GuestInvitationId == id, cancellationToken);
        return invitation.ToResponseDto(confirmedGuest);
    }

    public async Task<GuestInvitationResponseDto> SetInvitationActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        Guid coupleId = GetCoupleId();
        GuestInvitation invitation = await GetInvitationAsync(id, coupleId, cancellationToken);
        invitation.SetActive(isActive);
        await AddAuditAsync("GuestInvitationActiveChanged", "GuestInvitation", id, coupleId, cancellationToken);
        await guestRepository.SaveChangesAsync(cancellationToken);
        ConfirmedGuest? confirmedGuest = await guestRepository.QueryConfirmedGuests()
            .FirstOrDefaultAsync(x => x.GuestInvitationId == id, cancellationToken);
        return invitation.ToResponseDto(confirmedGuest);
    }

    public async Task DeleteInvitationAsync(Guid id, CancellationToken cancellationToken)
    {
        Guid coupleId = GetCoupleId();
        GuestInvitation invitation = await GetInvitationAsync(id, coupleId, cancellationToken);

        if (await guestRepository.QueryConfirmedGuests().AnyAsync(x => x.GuestInvitationId == id, cancellationToken))
        {
            invitation.SetActive(false);
            await AddAuditAsync("GuestInvitationDeactivated", "GuestInvitation", id, coupleId, cancellationToken);
        }
        else
        {
            guestRepository.RemoveInvitation(invitation);
            await AddAuditAsync("GuestInvitationDeleted", "GuestInvitation", id, coupleId, cancellationToken);
        }

        await guestRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<GuestInvitationImportResponseDto> ImportInvitationsAsync(Stream csvStream, CancellationToken cancellationToken)
    {
        Guid coupleId = GetCoupleId();
        List<string> csvNames = await ReadInvitationNamesAsync(csvStream, cancellationToken);
        Dictionary<string, string> uniqueNames = new(StringComparer.Ordinal);
        int skippedCount = 0;

        foreach (string rawName in csvNames)
        {
            string name = GuestNameNormalizer.Clean(rawName ?? string.Empty);

            if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
            {
                skippedCount++;
                continue;
            }

            if (!uniqueNames.TryAdd(GuestNameNormalizer.Normalize(name), name))
                skippedCount++;
        }

        string[] normalizedNames = uniqueNames.Keys.ToArray();
        HashSet<string> existingNames = (await guestRepository.QueryInvitations()
                .Where(x => x.CoupleId == coupleId && normalizedNames.Contains(x.NormalizedName))
                .Select(x => x.NormalizedName)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);
        List<GuestInvitation> invitations = uniqueNames
            .Where(x => !existingNames.Contains(x.Key))
            .Select(x => GuestInvitation.Create(coupleId, x.Value, x.Key))
            .ToList();
        skippedCount += existingNames.Count;

        await guestRepository.AddInvitationsAsync(invitations, cancellationToken);
        foreach (GuestInvitation invitation in invitations)
            await AddAuditAsync("GuestInvitationImported", "GuestInvitation", invitation.Id, coupleId, cancellationToken);
        await SaveAsync(ErrorCodes.GUEST_INVITATION_ALREADY_EXISTS, cancellationToken);
        return new GuestInvitationImportResponseDto { CreatedCount = invitations.Count, SkippedCount = skippedCount };
    }

    public async Task<PagedResult<GuestConfirmationResponseDto>> GetConfirmationsAsync(GuestConfirmationQueryDto queryDto, CancellationToken cancellationToken)
    {
        ValidatePagination(queryDto.Page, queryDto.PageSize);
        IQueryable<GuestConfirmation> query = FilterConfirmations(
            guestRepository.QueryConfirmations().Where(x => x.CoupleId == GetCoupleId()),
            queryDto);
        int totalCount = await query.CountAsync(cancellationToken);
        List<GuestConfirmation> confirmations = await query.OrderByDescending(x => x.ConfirmedAtUtc)
            .Skip((queryDto.Page - 1) * queryDto.PageSize)
            .Take(queryDto.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<GuestConfirmationResponseDto>
        {
            Items = confirmations.Select(x => x.ToResponseDto()).ToList(),
            TotalCount = totalCount,
            Page = queryDto.Page,
            PageSize = queryDto.PageSize
        };
    }

    public async Task<GuestConfirmationResponseDto> UpdateConfirmationAsync(Guid id, GuestConfirmationCreateDto dto, CancellationToken cancellationToken)
    {
        Guid coupleId = GetCoupleId();
        GuestConfirmation confirmation = await GetConfirmationAsync(id, coupleId, cancellationToken);
        List<ConfirmedGuest> preparedGuests = await PrepareGuestsAsync(
            id,
            coupleId,
            dto.Guests,
            id,
            cancellationToken);
        List<ConfirmedGuest> existingGuests = confirmation.Guests.ToList();
        Dictionary<Guid, ConfirmedGuest> existingRegistered = existingGuests
            .Where(x => x.GuestInvitationId.HasValue)
            .ToDictionary(x => x.GuestInvitationId!.Value);

        for (int index = 0; index < preparedGuests.Count; index++)
        {
            ConfirmedGuest guest = preparedGuests[index];

            if (!guest.GuestInvitationId.HasValue || !existingRegistered.TryGetValue(guest.GuestInvitationId.Value, out ConfirmedGuest? existingGuest))
                continue;

            existingGuest.Update(guest.Name, guest.NormalizedName, guest.Source, guest.IsSubmitter);
            preparedGuests[index] = existingGuest;
        }

        HashSet<Guid> retainedIds = preparedGuests.Select(x => x.Id).ToHashSet();
        HashSet<Guid> existingIds = existingGuests.Select(x => x.Id).ToHashSet();
        guestRepository.RemoveConfirmedGuests(existingGuests.Where(x => !retainedIds.Contains(x.Id)));
        await guestRepository.AddConfirmedGuestsAsync(
            preparedGuests.Where(x => !existingIds.Contains(x.Id)),
            cancellationToken);
        confirmation.ReplaceGuests(preparedGuests);
        await AddAuditAsync("GuestConfirmationUpdated", "GuestConfirmation", id, coupleId, cancellationToken);
        await SaveAsync(ErrorCodes.GUEST_INVITATION_ALREADY_CONFIRMED, cancellationToken);
        return await GetConfirmationResponseAsync(confirmation.Id, cancellationToken);
    }

    public async Task DeleteConfirmationAsync(Guid id, CancellationToken cancellationToken)
    {
        Guid coupleId = GetCoupleId();
        GuestConfirmation confirmation = await GetConfirmationAsync(id, coupleId, cancellationToken);
        guestRepository.RemoveConfirmation(confirmation);
        await AddAuditAsync("GuestConfirmationDeleted", "GuestConfirmation", id, coupleId, cancellationToken);
        await guestRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<byte[]> ExportConfirmationsCsvAsync(GuestConfirmationQueryDto queryDto, CancellationToken cancellationToken)
    {
        List<GuestConfirmation> confirmations = await FilterConfirmations(
                guestRepository.QueryConfirmations().Where(x => x.CoupleId == GetCoupleId()),
                queryDto)
            .OrderByDescending(x => x.ConfirmedAtUtc)
            .ToListAsync(cancellationToken);
        StringBuilder csv = new("\uFEFFEnvio;Confirmado em;Responsável;Convidado;Origem;É responsável\r\n");

        foreach (GuestConfirmation confirmation in confirmations)
        {
            ConfirmedGuest submitter = confirmation.Guests.First(x => x.IsSubmitter);

            foreach (ConfirmedGuest guest in confirmation.Guests.OrderByDescending(x => x.IsSubmitter).ThenBy(x => x.CreatedAtUtc))
            {
                csv.Append(confirmation.Id.ToString()).Append(';');
                csv.Append(confirmation.ConfirmedAtUtc.ToString("yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture)).Append(';');
                csv.Append(EscapeCsv(submitter.Name)).Append(';');
                csv.Append(EscapeCsv(guest.Name)).Append(';');
                csv.Append(EscapeCsv(guest.Source)).Append(';');
                csv.Append(guest.IsSubmitter ? "Sim" : "Não").Append("\r\n");
            }
        }

        await AddAuditAsync("GuestConfirmationsExported", "GuestConfirmation", Guid.Empty, GetCoupleId(), cancellationToken);

        if (operationalRepository is not null)
            await operationalRepository.SaveChangesAsync(cancellationToken);

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    private async Task<List<ConfirmedGuest>> PrepareGuestsAsync(
        Guid confirmationId,
        Guid coupleId,
        IReadOnlyCollection<GuestConfirmationGuestDto>? guestDtos,
        Guid? currentConfirmationId,
        CancellationToken cancellationToken)
    {
        List<GuestConfirmationGuestDto> requests = (guestDtos ?? []).ToList();
        if (requests.Count is < 1 or > 20 || requests.Count(x => x.IsSubmitter) != 1)
            throw new BadRequestException(ErrorCodes.INVALID_GUEST_CONFIRMATION);

        Guid[] invitationIds = requests
            .Where(x => x.GuestInvitationId.HasValue)
            .Select(x => x.GuestInvitationId!.Value)
            .ToArray();
        if (invitationIds.Distinct().Count() != invitationIds.Length)
            throw new BadRequestException(ErrorCodes.DUPLICATE_GUEST_NAME);

        List<GuestInvitation> invitationList = await guestRepository.QueryInvitations()
            .Where(x => x.CoupleId == coupleId && invitationIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
        Dictionary<Guid, GuestInvitation> invitations = invitationList.ToDictionary(x => x.Id);
        if (invitations.Count != invitationIds.Length)
            throw new NotFoundException(ErrorCodes.GUEST_INVITATION_NOT_FOUND);

        if (invitations.Values.Any(x => !x.IsActive))
            throw new ConflictException(ErrorCodes.GUEST_INVITATION_INACTIVE);

        bool alreadyConfirmed = await guestRepository.QueryConfirmedGuests()
            .AnyAsync(x => x.GuestInvitationId.HasValue &&
                           invitationIds.Contains(x.GuestInvitationId.Value) &&
                           (!currentConfirmationId.HasValue || x.GuestConfirmationId != currentConfirmationId.Value),
                cancellationToken);
        if (alreadyConfirmed)
            throw new ConflictException(ErrorCodes.GUEST_INVITATION_ALREADY_CONFIRMED);

        List<ConfirmedGuest> guests = [];
        foreach (GuestConfirmationGuestDto request in requests)
        {
            GuestInvitation? invitation = request.GuestInvitationId.HasValue
                ? invitations[request.GuestInvitationId.Value]
                : null;
            string name = invitation?.Name ?? CleanName(request.Name);
            string normalizedName = invitation?.NormalizedName ?? GuestNameNormalizer.Normalize(name);
            guests.Add(ConfirmedGuest.Create(
                confirmationId,
                invitation?.Id,
                name,
                normalizedName,
                invitation is null ? GuestConfirmationSources.FreeText : GuestConfirmationSources.RegisteredList,
                request.IsSubmitter));
        }

        if (guests.Select(x => x.NormalizedName).Distinct(StringComparer.Ordinal).Count() != guests.Count)
            throw new BadRequestException(ErrorCodes.DUPLICATE_GUEST_NAME);

        return guests;
    }

    private static IQueryable<GuestConfirmation> FilterConfirmations(IQueryable<GuestConfirmation> query, GuestConfirmationQueryDto queryDto)
    {
        if (!string.IsNullOrWhiteSpace(queryDto.Search))
        {
            string search = GuestNameNormalizer.Normalize(queryDto.Search);
            query = query.Where(x => x.Guests.Any(g => g.NormalizedName.Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(queryDto.Source))
        {
            if (!GuestConfirmationSources.Allowed.Contains(queryDto.Source))
                throw new BadRequestException(ErrorCodes.VALIDATION_ERROR);

            query = query.Where(x => x.Guests.Any(g => g.Source == queryDto.Source));
        }

        if (queryDto.FromUtc.HasValue)
            query = query.Where(x => x.ConfirmedAtUtc >= queryDto.FromUtc.Value);

        if (queryDto.ToUtc.HasValue)
            query = query.Where(x => x.ConfirmedAtUtc <= queryDto.ToUtc.Value);

        if (queryDto.FromUtc.HasValue && queryDto.ToUtc.HasValue && queryDto.ToUtc.Value < queryDto.FromUtc.Value)
            throw new BadRequestException(ErrorCodes.VALIDATION_ERROR);

        return query;
    }

    private async Task EnsureInvitationNameAvailableAsync(Guid coupleId, string normalizedName, Guid? currentId, CancellationToken cancellationToken)
    {
        if (await guestRepository.QueryInvitations().AnyAsync(x => x.CoupleId == coupleId && x.NormalizedName == normalizedName && (!currentId.HasValue || x.Id != currentId.Value), cancellationToken))
            throw new ConflictException(ErrorCodes.GUEST_INVITATION_ALREADY_EXISTS);
    }

    private async Task<GuestInvitation> GetInvitationAsync(Guid id, Guid coupleId, CancellationToken cancellationToken)
    {
        GuestInvitation invitation = await guestRepository.GetInvitationByIdAsync(id, cancellationToken)
                                     ?? throw new NotFoundException(ErrorCodes.GUEST_INVITATION_NOT_FOUND);

        if (invitation.CoupleId != coupleId)
            throw new NotFoundException(ErrorCodes.GUEST_INVITATION_NOT_FOUND);

        return invitation;
    }

    private async Task<GuestConfirmation> GetConfirmationAsync(Guid id, Guid coupleId, CancellationToken cancellationToken)
    {
        GuestConfirmation confirmation = await guestRepository.GetConfirmationByIdAsync(id, cancellationToken)
                                           ?? throw new NotFoundException(ErrorCodes.GUEST_CONFIRMATION_NOT_FOUND);

        if (confirmation.CoupleId != coupleId)
            throw new NotFoundException(ErrorCodes.GUEST_CONFIRMATION_NOT_FOUND);

        return confirmation;
    }

    private async Task<GuestConfirmationResponseDto> GetConfirmationResponseAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        GuestConfirmation confirmation = await guestRepository.QueryConfirmations()
            .FirstAsync(x => x.Id == id, cancellationToken);
        return confirmation.ToResponseDto();
    }

    private async Task SaveAsync(string conflictCode, CancellationToken cancellationToken)
    {
        try
        {
            await guestRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException(conflictCode);
        }
    }

    private async Task AddAuditAsync(string action, string entityType, Guid entityId, Guid coupleId, CancellationToken cancellationToken)
    {
        if (operationalRepository is null)
            return;

        await operationalRepository.AddAuditLogAsync(
            AuditLog.Create(requestContext?.UserId, coupleId, action, entityType, entityId == Guid.Empty ? string.Empty : entityId.ToString(), requestContext?.CorrelationId ?? string.Empty),
            cancellationToken);
    }

    private Guid GetCoupleId()
        => requestContext?.CoupleId ?? Couple.SingletonId;

    private static string CleanName(string value)
    {
        string name = GuestNameNormalizer.Clean(value ?? string.Empty);

        if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
            throw new BadRequestException(ErrorCodes.INVALID_GUEST_CONFIRMATION);

        return name;
    }

    private static async Task<List<string>> ReadInvitationNamesAsync(
        Stream csvStream,
        CancellationToken cancellationToken)
    {
        using StreamReader reader = new(csvStream, Encoding.UTF8, true, leaveOpen: true);
        string? header = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(header))
            throw new BadRequestException(ErrorCodes.INVALID_GUEST_IMPORT);

        char delimiter = header.Count(x => x == ';') >= header.Count(x => x == ',') ? ';' : ',';
        List<string> headers = ParseCsvRow(header, delimiter);
        int nameIndex = headers.FindIndex(x => GuestNameNormalizer.Normalize(x) == "NOME");
        if (nameIndex < 0)
            throw new BadRequestException(ErrorCodes.INVALID_GUEST_IMPORT);

        List<string> names = [];
        int rowCount = 0;
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            rowCount++;
            if (rowCount > 1000)
                throw new BadRequestException(ErrorCodes.INVALID_GUEST_IMPORT);

            List<string> values = ParseCsvRow(line, delimiter);
            names.Add(nameIndex < values.Count ? values[nameIndex] : string.Empty);
        }

        return names;
    }

    private static List<string> ParseCsvRow(string line, char delimiter)
    {
        List<string> values = [];
        StringBuilder current = new();
        bool quoted = false;

        for (int index = 0; index < line.Length; index++)
        {
            char character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == delimiter && !quoted)
            {
                values.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }

        values.Add(current.ToString().Trim());
        return values;
    }

    private static void ValidatePagination(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            throw new BadRequestException(ErrorCodes.VALIDATION_ERROR);
    }

    private static string EscapeCsv(string value)
        => $"\"{value.Replace("\"", "\"\"")}\"";
}
