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

public sealed class ContributionService(
    IContributionRepository contributionRepository,
    IGiftRepository giftRepository,
    IPaymentRepository paymentRepository,
    ICoupleRepository coupleRepository,
    IApplicationCacheService cacheService,
    IRequestContext? requestContext = null,
    IOperationalRepository? operationalRepository = null) : IContributionService
{
    public async Task<PagedResult<ContributionResponseDto>> GetAllAsync(
        ContributionQueryParams queryParams,
        CancellationToken cancellationToken)
    {
        IQueryable<Contribution> query = contributionRepository.Query()
            .Where(x => x.CoupleId == Couple.SingletonId && x.Status == ContributionStatus.Paid);

        if (queryParams.GiftId.HasValue)
            query = query.Where(x => x.GiftId == queryParams.GiftId.Value);

        int totalCount = await query.CountAsync(cancellationToken);
        List<Contribution> contributions = await query
            .OrderByDescending(x => x.PaidAt)
            .Skip((queryParams.Page - 1) * queryParams.PageSize)
            .Take(queryParams.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ContributionResponseDto>
        {
            Items = contributions.Select(x => x.ToPublicResponseDto()).ToList(),
            TotalCount = totalCount,
            Page = queryParams.Page,
            PageSize = queryParams.PageSize
        };
    }

    public async Task<ContributionResponseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        Contribution entity = await contributionRepository.GetByIdAsync(id, cancellationToken)
                              ?? throw new NotFoundException(ErrorCodes.CONTRIBUTION_NOT_FOUND);

        if (entity.CoupleId != Couple.SingletonId || entity.Status != ContributionStatus.Paid)
            throw new NotFoundException(ErrorCodes.CONTRIBUTION_NOT_FOUND);

        return entity.ToPublicResponseDto();
    }

    public async Task<PagedResult<ContributionResponseDto>> GetAllAdminAsync(
        ContributionAdminQueryParams queryParams,
        CancellationToken cancellationToken)
    {
        IQueryable<Contribution> query = contributionRepository.Query();
        Guid? coupleId = GetAdministrativeCoupleId();
        if (coupleId.HasValue)
            query = query.Where(x => x.CoupleId == coupleId.Value);

        ValidateDateRange(queryParams.FromUtc, queryParams.ToUtc, 366);

        if (!string.IsNullOrWhiteSpace(queryParams.Search))
        {
            string search = queryParams.Search.Trim();
            query = query.Where(x => x.ContributorName.Contains(search) ||
                                     x.Gift.Name.Contains(search) ||
                                     x.OrderId.Contains(search));
        }

        if (queryParams.GiftId.HasValue)
            query = query.Where(x => x.GiftId == queryParams.GiftId.Value);

        if (!string.IsNullOrWhiteSpace(queryParams.Status))
        {
            if (!ContributionStatus.Allowed.Contains(queryParams.Status))
                throw new BadRequestException(ErrorCodes.INVALID_CONTRIBUTION_STATUS);

            query = query.Where(x => x.Status == queryParams.Status);
        }

        if (queryParams.HasMessage == true)
            query = query.Where(x => x.Message != string.Empty);

        if (!string.IsNullOrWhiteSpace(queryParams.PaymentMethod))
        {
            string method = NormalizePaymentMethod(queryParams.PaymentMethod);
            query = query.Where(x => x.PaymentMethod == method);
        }

        if (queryParams.FromUtc.HasValue)
            query = query.Where(x => x.CreatedAtUtc >= queryParams.FromUtc.Value);

        if (queryParams.ToUtc.HasValue)
            query = query.Where(x => x.CreatedAtUtc <= queryParams.ToUtc.Value);

        if (queryParams.Archived.HasValue)
            query = queryParams.Archived.Value
                ? query.Where(x => x.MessageArchivedAtUtc != null)
                : query.Where(x => x.MessageArchivedAtUtc == null);

        int totalCount = await query.CountAsync(cancellationToken);
        IQueryable<Contribution> orderedQuery = string.Equals(queryParams.OrderDir, "asc", StringComparison.OrdinalIgnoreCase)
            ? query.OrderBy(x => x.CreatedAtUtc)
            : query.OrderByDescending(x => x.CreatedAtUtc);
        List<Contribution> contributions = await orderedQuery
            .Include(x => x.Gift)
            .Skip((queryParams.Page - 1) * queryParams.PageSize)
            .Take(queryParams.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ContributionResponseDto>
        {
            Items = contributions.Select(x => x.ToResponseDto()).ToList(),
            TotalCount = totalCount,
            Page = queryParams.Page,
            PageSize = queryParams.PageSize
        };
    }

    public async Task<ContributionResponseDto> CreateAsync(ContributionCreateDto dto, CancellationToken cancellationToken)
    {
        if (!ContributionStatus.Allowed.Contains(dto.Status))
            throw new BadRequestException(ErrorCodes.INVALID_CONTRIBUTION_STATUS);

        Gift gift = await giftRepository.GetByIdWithContributionsAsync(dto.GiftId, cancellationToken)
                    ?? throw new NotFoundException(ErrorCodes.GIFT_NOT_FOUND);
        EnsureGiftAccess(gift.CoupleId);

        if (gift.RemainingAmount <= 0 && !await CoupleAllowsUnlimitedPurchasesAsync(cancellationToken))
            throw new ConflictException(ErrorCodes.GIFT_UNAVAILABLE);

        if (dto.Status == ContributionStatus.Paid && !await CoupleAllowsUnlimitedPurchasesAsync(cancellationToken))
        {
            DateTime now = DateTime.UtcNow;
            decimal reservedAmount = await paymentRepository.Query()
                .Where(x => x.GiftId == gift.Id &&
                            !x.ContributionCreated &&
                            x.ExpiresAt > now &&
                            PaymentStatuses.Reserving.Contains(x.Status))
                .SumAsync(x => x.Amount, cancellationToken);
            decimal remainingAmount = Math.Max(gift.RemainingAmount - reservedAmount, 0);

            if (dto.Amount > remainingAmount || (!gift.AllowPartialContribution && dto.Amount < remainingAmount))
                throw new ConflictException(ErrorCodes.GIFT_UNAVAILABLE);
        }

        Contribution entity = dto.ToEntity(gift.CoupleId);

        if (entity.Status == ContributionStatus.Paid)
            entity.UpdateStatus(ContributionStatus.Paid, dto.PaidAt);

        await contributionRepository.AddAsync(entity, cancellationToken);
        await contributionRepository.SaveChangesAsync(cancellationToken);
        cacheService.Invalidate();

        return entity.ToResponseDto();
    }

    public async Task UpdateStatusAsync(Guid id, string status, DateTime paidAt, CancellationToken cancellationToken)
    {
        if (!ContributionStatus.Allowed.Contains(status))
            throw new BadRequestException(ErrorCodes.INVALID_CONTRIBUTION_STATUS);

        Contribution entity = await contributionRepository.GetByIdAsync(id, cancellationToken)
                              ?? throw new NotFoundException(ErrorCodes.CONTRIBUTION_NOT_FOUND);
        EnsureCoupleAccess(entity.CoupleId);

        if (status == ContributionStatus.Paid && entity.Status != ContributionStatus.Paid &&
            !await CoupleAllowsUnlimitedPurchasesAsync(cancellationToken))
        {
            Gift gift = await giftRepository.GetByIdWithContributionsAsync(entity.GiftId, cancellationToken)
                        ?? throw new NotFoundException(ErrorCodes.GIFT_NOT_FOUND);
            DateTime now = DateTime.UtcNow;
            decimal reservedAmount = await paymentRepository.Query()
                .Where(x => x.GiftId == gift.Id &&
                            !x.ContributionCreated &&
                            x.ExpiresAt > now &&
                            PaymentStatuses.Reserving.Contains(x.Status))
                .SumAsync(x => x.Amount, cancellationToken);
            decimal remainingAmount = Math.Max(gift.RemainingAmount - reservedAmount, 0);

            if (entity.Amount > remainingAmount || (!gift.AllowPartialContribution && entity.Amount < remainingAmount))
                throw new ConflictException(ErrorCodes.GIFT_UNAVAILABLE);
        }

        entity.UpdateStatus(status, paidAt);
        await contributionRepository.SaveChangesAsync(cancellationToken);
        cacheService.Invalidate();
    }

    public async Task<ContributionResponseDto> SetMessageReadAsync(Guid id, bool read, CancellationToken cancellationToken)
    {
        Contribution entity = await contributionRepository.GetByIdAsync(id, cancellationToken)
                              ?? throw new NotFoundException(ErrorCodes.CONTRIBUTION_NOT_FOUND);
        EnsureCoupleAccess(entity.CoupleId);
        entity.SetMessageRead(read);
        await AddAuditAsync("ContributionMessageReadChanged", entity, cancellationToken);
        await contributionRepository.SaveChangesAsync(cancellationToken);
        return entity.ToResponseDto();
    }

    public async Task<ContributionResponseDto> SetMessageArchivedAsync(Guid id, bool archived, CancellationToken cancellationToken)
    {
        Contribution entity = await contributionRepository.GetByIdAsync(id, cancellationToken)
                              ?? throw new NotFoundException(ErrorCodes.CONTRIBUTION_NOT_FOUND);
        EnsureCoupleAccess(entity.CoupleId);
        entity.SetMessageArchived(archived);
        await AddAuditAsync("ContributionMessageArchiveChanged", entity, cancellationToken);
        await contributionRepository.SaveChangesAsync(cancellationToken);
        return entity.ToResponseDto();
    }

    public async Task<byte[]> ExportCsvAsync(ContributionAdminQueryParams queryParams, CancellationToken cancellationToken)
    {
        queryParams.ToUtc ??= DateTime.UtcNow;
        queryParams.FromUtc ??= queryParams.ToUtc.Value.AddDays(-366);
        ValidateDateRange(queryParams.FromUtc, queryParams.ToUtc, 366);
        queryParams.Page = 1;
        queryParams.PageSize = 100;
        PagedResult<ContributionResponseDto> result = await GetAllAdminAsync(queryParams, cancellationToken);
        if (operationalRepository is not null)
        {
            await operationalRepository.AddAuditLogAsync(
                AuditLog.Create(requestContext?.UserId, requestContext?.CoupleId, "ContributionsExported", "Contribution", string.Empty, requestContext?.CorrelationId ?? string.Empty),
                cancellationToken);
            await operationalRepository.SaveChangesAsync(cancellationToken);
        }
        StringBuilder csv = new("\uFEFFPedido;Presente;Categoria;Convidado;E-mail;Valor;Mensagem;Status;Método;Data da contribuição;Data da aprovação\r\n");

        foreach (ContributionResponseDto item in result.Items)
        {
            csv.Append(EscapeCsv(item.OrderId));
            csv.Append(';');
            csv.Append(EscapeCsv(item.GiftName));
            csv.Append(';');
            csv.Append(EscapeCsv(item.Category));
            csv.Append(';');
            csv.Append(EscapeCsv(item.GuestName));
            csv.Append(';');
            csv.Append(EscapeCsv(item.GuestEmail));
            csv.Append(';');
            csv.Append(item.Amount.ToString("0.00", new CultureInfo("pt-BR")));
            csv.Append(';');
            csv.Append(EscapeCsv(item.Message));
            csv.Append(';');
            csv.Append(EscapeCsv(item.Status));
            csv.Append(';');
            csv.Append(EscapeCsv(item.PaymentMethod));
            csv.Append(';');
            csv.Append(item.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            csv.Append(';');
            csv.Append(item.PaidAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            csv.Append("\r\n");
        }

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    private static string EscapeCsv(string? value)
        => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";

    private Guid? GetAdministrativeCoupleId()
        => requestContext?.IsSuperAdmin == true ? null : requestContext?.CoupleId ?? Couple.SingletonId;

    private void EnsureCoupleAccess(Guid coupleId)
    {
        Guid? allowedCoupleId = GetAdministrativeCoupleId();
        if (allowedCoupleId.HasValue && coupleId != allowedCoupleId.Value)
            throw new NotFoundException(ErrorCodes.CONTRIBUTION_NOT_FOUND);
    }

    private void EnsureGiftAccess(Guid coupleId)
    {
        Guid? allowedCoupleId = GetAdministrativeCoupleId();
        if (allowedCoupleId.HasValue && coupleId != allowedCoupleId.Value)
            throw new NotFoundException(ErrorCodes.GIFT_NOT_FOUND);
    }

    private static string NormalizePaymentMethod(string value)
        => value.Trim() switch
        {
            "Pix" => "pix",
            "CreditCard" => "credit_card",
            "DebitCard" => "debit_card",
            "pix" or "credit_card" or "debit_card" => value.Trim(),
            _ => throw new BadRequestException(ErrorCodes.VALIDATION_ERROR)
        };

    private static void ValidateDateRange(DateTime? fromUtc, DateTime? toUtc, int maximumDays)
    {
        if (fromUtc.HasValue && toUtc.HasValue &&
            (toUtc.Value < fromUtc.Value || toUtc.Value - fromUtc.Value > TimeSpan.FromDays(maximumDays)))
        {
            throw new BadRequestException(ErrorCodes.VALIDATION_ERROR);
        }
    }

    private async Task AddAuditAsync(string action, Contribution contribution, CancellationToken cancellationToken)
    {
        if (operationalRepository is null)
            return;

        await operationalRepository.AddAuditLogAsync(
            AuditLog.Create(requestContext?.UserId, contribution.CoupleId, action, "Contribution", contribution.Id.ToString(), requestContext?.CorrelationId ?? string.Empty),
            cancellationToken);
    }

    private async Task<bool> CoupleAllowsUnlimitedPurchasesAsync(CancellationToken cancellationToken)
    {
        Couple? couple = await coupleRepository.GetAsync(false, cancellationToken);
        return GiftDisplayModes.AllowsUnlimitedPurchases(couple?.GiftDisplayMode);
    }
}
