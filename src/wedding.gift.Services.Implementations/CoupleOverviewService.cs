using Microsoft.EntityFrameworkCore;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Contracts;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Services.Implementations;

public sealed class CoupleOverviewService(
    IGiftRepository giftRepository,
    IContributionRepository contributionRepository,
    IPaymentRepository paymentRepository,
    IRequestContext requestContext) : ICoupleOverviewService
{
    public async Task<CoupleOverviewDto> GetAsync(int days, CancellationToken cancellationToken)
    {
        Guid coupleId = requestContext.CoupleId ?? Couple.SingletonId;
        DateTime fromUtc = DateTime.UtcNow.Date.AddDays(-(days - 1));
        List<Gift> gifts = await giftRepository.QueryWithContributions().Where(x => x.CoupleId == coupleId).ToListAsync(cancellationToken);
        List<Contribution> contributions = await contributionRepository.Query().Include(x => x.Gift).Where(x => x.CoupleId == coupleId).ToListAsync(cancellationToken);
        List<Payment> payments = await paymentRepository.Query().Where(x => x.CoupleId == coupleId).ToListAsync(cancellationToken);
        HashSet<Guid> approvedContributionIds = payments
            .Where(x => PaymentStatuses.IsSettled(x.Status) && x.ContributionCreated && x.ContributionId.HasValue)
            .Select(x => x.ContributionId!.Value).ToHashSet();
        List<Contribution> approved = contributions.Where(x => approvedContributionIds.Contains(x.Id) && x.Status == ContributionStatus.Paid).ToList();
        Dictionary<Guid, decimal> raisedByGift = approved.GroupBy(x => x.GiftId).ToDictionary(x => x.Key, x => x.Sum(c => c.NetAmount));

        return new CoupleOverviewDto
        {
            TotalRaised = approved.Sum(x => x.NetAmount),
            Goal = gifts.Sum(x => x.Total),
            TotalGifts = gifts.Count,
            CompletedGifts = gifts.Count(x => raisedByGift.GetValueOrDefault(x.Id) >= x.Total),
            GiftsWithoutContribution = gifts.Count(x => !raisedByGift.ContainsKey(x.Id)),
            ApprovedContributions = approved.Count,
            PendingContributions = payments.Count(x => PaymentStatuses.Reserving.Contains(x.Status)),
            FailedContributions = payments.Count(x => !PaymentStatuses.IsSettled(x.Status) && !PaymentStatuses.Reserving.Contains(x.Status)),
            UniqueContributors = approved.Select(x => string.IsNullOrWhiteSpace(x.GuestEmail) ? x.ContributorName : x.GuestEmail).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            DailyApprovedAmounts = Enumerable.Range(0, days).Select(offset =>
            {
                DateTime date = fromUtc.AddDays(offset);
                return new DailyApprovedAmountDto { DateUtc = date, Amount = approved.Where(x => x.PaidAt.Date == date).Sum(x => x.NetAmount) };
            }).ToList()
        };
    }
}
