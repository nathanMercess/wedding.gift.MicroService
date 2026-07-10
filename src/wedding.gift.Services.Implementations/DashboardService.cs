using Microsoft.Extensions.Logging;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Contracts;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Exceptions;

namespace wedding.gift.Services.Implementations;

public sealed class DashboardService(
    IGiftRepository giftRepository,
    IContributionRepository contributionRepository,
    IPaymentRepository paymentRepository,
    IApiRequestLogRepository apiRequestLogRepository,
    ILogger<DashboardService> logger) : IDashboardService
{
    public async Task<DashboardResponseDto> GetAsync(DashboardQueryDto query, CancellationToken cancellationToken)
    {
        if (query.Days < 1 || query.Days > 365)
            throw new BadRequestException(ErrorCodes.INVALID_DASHBOARD_DAYS);

        if (query.RecentItems < 1 || query.RecentItems > 50)
            throw new BadRequestException(ErrorCodes.INVALID_DASHBOARD_RECENT_ITEMS);

        DateTime now = DateTime.UtcNow;
        DateTime fromUtc = now.Date.AddDays(-(query.Days - 1));

        List<Gift> gifts = (await giftRepository.GetAllWithContributionsAsync(cancellationToken)).ToList();
        List<Contribution> contributions = (await contributionRepository.GetAllAsync(cancellationToken)).ToList();
        List<Payment> payments = (await paymentRepository.GetAllAsync(cancellationToken)).ToList();
        List<ApiRequestLog> requestLogs = await GetRequestLogsAsync(fromUtc, now, cancellationToken);

        Dictionary<Guid, string> giftNames = gifts.ToDictionary(x => x.Id, x => x.Name);
        List<DashboardGiftFundingDto> giftFunding = BuildGiftFunding(gifts);
        List<Contribution> paidContributions = contributions
            .Where(x => string.Equals(x.Status, ContributionStatus.Paid, StringComparison.OrdinalIgnoreCase))
            .ToList();
        List<Contribution> pendingContributions = contributions
            .Where(x => string.Equals(x.Status, ContributionStatus.Pending, StringComparison.OrdinalIgnoreCase))
            .ToList();
        List<Contribution> cancelledContributions = contributions
            .Where(x => string.Equals(x.Status, ContributionStatus.Cancelled, StringComparison.OrdinalIgnoreCase))
            .ToList();
        List<Contribution> periodPaidContributions = paidContributions
            .Where(x => x.PaidAt >= fromUtc && x.PaidAt <= now)
            .ToList();

        List<Payment> approvedPayments = payments.Where(x => IsApprovedPayment(x.Status)).ToList();
        List<Payment> pendingPayments = payments.Where(x => IsPendingPayment(x.Status)).ToList();
        List<Payment> failedPayments = payments.Where(x => IsFailedPayment(x.Status)).ToList();
        int categorizedPaymentCount = approvedPayments.Count + pendingPayments.Count + failedPayments.Count;
        int approvedWithoutContribution = approvedPayments.Count(x => !x.ContributionCreated);
        int serverErrorRequests = requestLogs.Count(x => x.StatusCode >= 500);
        int slowRequests = requestLogs.Count(x => x.DurationMilliseconds >= 1000);
        List<DashboardMessageDto> contributionMessages = BuildContributionMessages(contributions, giftNames);
        List<DashboardMessageDto> paymentMessages = BuildPaymentIntentMessages(payments, giftNames);
        List<DashboardMessageDto> allMessages = contributionMessages
            .Concat(paymentMessages)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToList();

        return new DashboardResponseDto
        {
            GeneratedAtUtc = now,
            Period = new DashboardPeriodDto
            {
                FromUtc = fromUtc,
                ToUtc = now,
                Days = query.Days
            },
            Overview = new DashboardOverviewDto
            {
                TotalRaised = paidContributions.Sum(x => x.Amount),
                TotalGoal = gifts.Sum(x => x.Total),
                FundingPercent = CalculatePercent(paidContributions.Sum(x => x.Amount), gifts.Sum(x => x.Total)),
                TotalGifts = gifts.Count,
                FullyFundedGifts = giftFunding.Count(x => x.FullyFunded),
                PaidContributions = paidContributions.Count,
                UniqueContributors = CountUniqueContributors(paidContributions),
                ApprovedPayments = approvedPayments.Count,
                FailedPayments = failedPayments.Count
            },
            Gifts = BuildGiftSummary(gifts, giftFunding),
            Contributions = new DashboardContributionSummaryDto
            {
                Total = contributions.Count,
                Paid = paidContributions.Count,
                Pending = pendingContributions.Count,
                Cancelled = cancelledContributions.Count,
                PaidAmount = paidContributions.Sum(x => x.Amount),
                PendingAmount = pendingContributions.Sum(x => x.Amount),
                CancelledAmount = cancelledContributions.Sum(x => x.Amount),
                AveragePaidAmount = paidContributions.Count == 0 ? 0 : Math.Round(paidContributions.Average(x => x.Amount), 2),
                UniqueContributors = CountUniqueContributors(paidContributions),
                MessagesCount = contributionMessages.Count,
                PeriodPaidCount = periodPaidContributions.Count,
                PeriodPaidAmount = periodPaidContributions.Sum(x => x.Amount)
            },
            Payments = new DashboardPaymentSummaryDto
            {
                Total = payments.Count,
                Approved = approvedPayments.Count,
                Pending = pendingPayments.Count,
                Failed = failedPayments.Count,
                Other = payments.Count - categorizedPaymentCount,
                Pix = payments.Count(x => string.Equals(x.Method, "pix", StringComparison.OrdinalIgnoreCase)),
                Card = payments.Count(x => IsCardPayment(x.Method)),
                ApprovedWithoutContribution = approvedWithoutContribution,
                ApprovedAmount = approvedPayments.Sum(x => x.Amount),
                PendingAmount = pendingPayments.Sum(x => x.Amount),
                FailedAmount = failedPayments.Sum(x => x.Amount),
                SuccessRate = CalculatePercent(approvedPayments.Count, payments.Count),
                FailureRate = CalculatePercent(failedPayments.Count, payments.Count)
            },
            Messages = new DashboardMessageSummaryDto
            {
                Total = allMessages.Count,
                ContributionMessages = contributionMessages.Count,
                PaymentIntentMessages = paymentMessages.Count,
                LatestMessageAtUtc = allMessages.FirstOrDefault()?.CreatedAtUtc
            },
            Requests = BuildRequestSummary(requestLogs),
            Monitoring = new DashboardMonitoringDto
            {
                DatabaseStatus = "Online",
                ApplicationLogsStatus = "ApiRequestLogs ativo",
                MetricsStatus = "Pendente de validacao",
                PendingPayments = pendingPayments.Count,
                PendingPixPayments = pendingPayments.Count(x => string.Equals(x.Method, "pix", StringComparison.OrdinalIgnoreCase)),
                FailedPayments = failedPayments.Count,
                ApprovedPaymentsWithoutContribution = approvedWithoutContribution,
                ServerErrorRequests = serverErrorRequests,
                SlowRequests = slowRequests,
                AverageRequestDurationMilliseconds = CalculateAverageDuration(requestLogs),
                LastPaymentAtUtc = payments.OrderByDescending(x => x.CreatedAt).FirstOrDefault()?.CreatedAt,
                LastContributionAtUtc = contributions.OrderByDescending(x => x.PaidAt).FirstOrDefault()?.PaidAt,
                LastRequestAtUtc = requestLogs.OrderByDescending(x => x.StartedAtUtc).FirstOrDefault()?.StartedAtUtc,
                Notes =
                [
                    "Falhas e sucessos representam pagamentos persistidos no banco.",
                    "Requests da API sao persistidos em ApiRequestLogs sem body, tokens, cookies ou headers sensiveis.",
                    "Metricas APM e traces distribuidos: Pendente de validacao."
                ]
            },
            ContributionsByDay = BuildContributionsByDay(periodPaidContributions, fromUtc, query.Days),
            PaymentsByStatus = BuildPaymentsByStatus(payments),
            PaymentsByMethod = BuildPaymentsByMethod(payments),
            GiftsByCategory = BuildGiftsByCategory(giftFunding),
            RequestsByStatus = BuildRequestsByStatus(requestLogs),
            RequestsByPath = BuildRequestsByPath(requestLogs)
                .Take(query.RecentItems)
                .ToList(),
            TopGiftsByRaised = giftFunding
                .OrderByDescending(x => x.Raised)
                .ThenBy(x => x.GiftName)
                .Take(query.RecentItems)
                .ToList(),
            RecentMessages = allMessages.Take(query.RecentItems).ToList(),
            RecentRequests = requestLogs
                .OrderByDescending(x => x.StartedAtUtc)
                .Take(query.RecentItems)
                .Select(ToRequestActivityDto)
                .ToList(),
            RecentPayments = payments
                .OrderByDescending(x => x.UpdatedAt)
                .ThenByDescending(x => x.CreatedAt)
                .Take(query.RecentItems)
                .Select(x => ToPaymentActivityDto(x, giftNames))
                .ToList(),
            RecentFailedPayments = failedPayments
                .OrderByDescending(x => x.UpdatedAt)
                .ThenByDescending(x => x.CreatedAt)
                .Take(query.RecentItems)
                .Select(x => ToPaymentActivityDto(x, giftNames))
                .ToList(),
            RecentContributions = contributions
                .OrderByDescending(x => x.PaidAt)
                .Take(query.RecentItems)
                .Select(x => ToContributionActivityDto(x, giftNames))
                .ToList()
        };
    }

    private async Task<List<ApiRequestLog>> GetRequestLogsAsync(
        DateTime fromUtc,
        DateTime now,
        CancellationToken cancellationToken)
    {
        try
        {
            return (await apiRequestLogRepository.GetByStartedAtRangeAsync(fromUtc, now, cancellationToken)).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao carregar logs de request para o dashboard.");
            return [];
        }
    }

    private static DashboardRequestSummaryDto BuildRequestSummary(IReadOnlyCollection<ApiRequestLog> requestLogs)
    {
        int successful = requestLogs.Count(x => x.StatusCode < 400);
        int clientErrors = requestLogs.Count(x => x.StatusCode >= 400 && x.StatusCode < 500);
        int serverErrors = requestLogs.Count(x => x.StatusCode >= 500);

        return new DashboardRequestSummaryDto
        {
            Total = requestLogs.Count,
            Successful = successful,
            ClientErrors = clientErrors,
            ServerErrors = serverErrors,
            Authenticated = requestLogs.Count(x => x.IsAuthenticated),
            Anonymous = requestLogs.Count(x => !x.IsAuthenticated),
            SlowRequests = requestLogs.Count(x => x.DurationMilliseconds >= 1000),
            SuccessRate = CalculatePercent(successful, requestLogs.Count),
            ClientErrorRate = CalculatePercent(clientErrors, requestLogs.Count),
            ServerErrorRate = CalculatePercent(serverErrors, requestLogs.Count),
            AverageDurationMilliseconds = CalculateAverageDuration(requestLogs),
            MaxDurationMilliseconds = requestLogs.Count == 0 ? 0 : requestLogs.Max(x => x.DurationMilliseconds),
            LastRequestAtUtc = requestLogs.OrderByDescending(x => x.StartedAtUtc).FirstOrDefault()?.StartedAtUtc
        };
    }

    private static DashboardGiftSummaryDto BuildGiftSummary(
        IReadOnlyCollection<Gift> gifts,
        IReadOnlyCollection<DashboardGiftFundingDto> giftFunding)
    {
        decimal raisedAmount = giftFunding.Sum(x => x.Raised);
        decimal goalAmount = gifts.Sum(x => x.Total);

        return new DashboardGiftSummaryDto
        {
            Total = gifts.Count,
            Available = gifts.Count(x => x.Available),
            Unavailable = gifts.Count(x => !x.Available),
            FullyFunded = giftFunding.Count(x => x.FullyFunded),
            PartiallyFunded = giftFunding.Count(x => x.Raised > 0 && !x.FullyFunded),
            WithoutContributions = giftFunding.Count(x => x.Raised == 0),
            GoalAmount = goalAmount,
            RaisedAmount = raisedAmount,
            RemainingAmount = giftFunding.Sum(x => x.Remaining),
            AverageFundingPercent = giftFunding.Count == 0 ? 0 : Math.Round(giftFunding.Average(x => x.FundingPercent), 2)
        };
    }

    private static List<DashboardGiftFundingDto> BuildGiftFunding(IEnumerable<Gift> gifts)
    {
        return gifts
            .Select(gift =>
            {
                List<Contribution> paidContributions = gift.Contributions
                    .Where(x => string.Equals(x.Status, ContributionStatus.Paid, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                decimal raised = paidContributions.Sum(x => x.Amount);
                decimal remaining = Math.Max(gift.Total - raised, 0);

                return new DashboardGiftFundingDto
                {
                    GiftId = gift.Id,
                    GiftName = gift.Name,
                    Category = NormalizeCategory(gift.Category),
                    Total = gift.Total,
                    Raised = raised,
                    Remaining = remaining,
                    FundingPercent = CalculatePercent(raised, gift.Total),
                    PaidContributions = paidContributions.Count,
                    Available = gift.Available,
                    FullyFunded = raised >= gift.Total && gift.Total > 0
                };
            })
            .ToList();
    }

    private static List<DashboardTimeSeriesPointDto> BuildContributionsByDay(
        IReadOnlyCollection<Contribution> contributions,
        DateTime fromUtc,
        int days)
    {
        Dictionary<DateTime, DashboardTimeSeriesPointDto> grouped = contributions
            .GroupBy(x => x.PaidAt.Date)
            .ToDictionary(x => x.Key, x => new DashboardTimeSeriesPointDto
            {
                Count = x.Count(),
                Amount = x.Sum(c => c.Amount)
            });

        return Enumerable.Range(0, days)
            .Select(offset =>
            {
                DateTime date = fromUtc.Date.AddDays(offset);
                grouped.TryGetValue(date, out DashboardTimeSeriesPointDto item);

                return new DashboardTimeSeriesPointDto
                {
                    DateUtc = date,
                    Count = item?.Count ?? 0,
                    Amount = item?.Amount ?? 0
                };
            })
            .ToList();
    }

    private static List<DashboardStatusChartDto> BuildPaymentsByStatus(IEnumerable<Payment> payments)
    {
        return payments
            .GroupBy(x => NormalizeStatus(x.Status))
            .Select(x => new DashboardStatusChartDto
            {
                Status = x.Key,
                Count = x.Count(),
                Amount = x.Sum(p => p.Amount)
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Status)
            .ToList();
    }

    private static List<DashboardPaymentMethodChartDto> BuildPaymentsByMethod(IEnumerable<Payment> payments)
    {
        return payments
            .GroupBy(x => NormalizeMethod(x.Method))
            .Select(x => new DashboardPaymentMethodChartDto
            {
                Method = x.Key,
                Count = x.Count(),
                Amount = x.Sum(p => p.Amount)
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Method)
            .ToList();
    }

    private static List<DashboardCategoryChartDto> BuildGiftsByCategory(IEnumerable<DashboardGiftFundingDto> giftFunding)
    {
        return giftFunding
            .GroupBy(x => NormalizeCategory(x.Category))
            .Select(x => new DashboardCategoryChartDto
            {
                Category = x.Key,
                Count = x.Count(),
                GoalAmount = x.Sum(g => g.Total),
                RaisedAmount = x.Sum(g => g.Raised)
            })
            .OrderByDescending(x => x.RaisedAmount)
            .ThenBy(x => x.Category)
            .ToList();
    }

    private static List<DashboardRequestStatusChartDto> BuildRequestsByStatus(IEnumerable<ApiRequestLog> requestLogs)
    {
        return requestLogs
            .GroupBy(x => GetStatusGroup(x.StatusCode))
            .Select(x => new DashboardRequestStatusChartDto
            {
                StatusGroup = x.Key,
                Count = x.Count(),
                AverageDurationMilliseconds = CalculateAverageDuration(x)
            })
            .OrderBy(x => x.StatusGroup)
            .ToList();
    }

    private static List<DashboardRequestPathChartDto> BuildRequestsByPath(IEnumerable<ApiRequestLog> requestLogs)
    {
        return requestLogs
            .GroupBy(x => (x.Method, x.Path))
            .Select(x => new DashboardRequestPathChartDto
            {
                Method = x.Key.Method,
                Path = x.Key.Path,
                Count = x.Count(),
                ServerErrors = x.Count(r => r.StatusCode >= 500),
                AverageDurationMilliseconds = CalculateAverageDuration(x),
                MaxDurationMilliseconds = x.Max(r => r.DurationMilliseconds)
            })
            .OrderByDescending(x => x.Count)
            .ThenByDescending(x => x.AverageDurationMilliseconds)
            .ThenBy(x => x.Path)
            .ToList();
    }

    private static List<DashboardMessageDto> BuildContributionMessages(
        IEnumerable<Contribution> contributions,
        IReadOnlyDictionary<Guid, string> giftNames)
    {
        return contributions
            .Where(x => !string.IsNullOrWhiteSpace(x.Message))
            .Select(x => new DashboardMessageDto
            {
                Source = "Contribution",
                SourceId = x.Id,
                GiftId = x.GiftId,
                GiftName = GetGiftName(giftNames, x.GiftId),
                ContributorName = x.ContributorName,
                Message = x.Message?.Trim() ?? string.Empty,
                Amount = x.Amount,
                Status = x.Status,
                CreatedAtUtc = x.PaidAt
            })
            .ToList();
    }

    private static List<DashboardMessageDto> BuildPaymentIntentMessages(
        IEnumerable<Payment> payments,
        IReadOnlyDictionary<Guid, string> giftNames)
    {
        return payments
            .Where(x => !x.ContributionCreated && !string.IsNullOrWhiteSpace(x.Message))
            .Select(x => new DashboardMessageDto
            {
                Source = "PaymentIntent",
                SourceId = x.Id,
                GiftId = x.GiftId,
                GiftName = GetGiftName(giftNames, x.GiftId),
                ContributorName = x.ContributorName,
                Message = x.Message?.Trim() ?? string.Empty,
                Amount = x.Amount,
                Status = x.Status,
                CreatedAtUtc = x.CreatedAt
            })
            .ToList();
    }

    private static DashboardPaymentActivityDto ToPaymentActivityDto(
        Payment payment,
        IReadOnlyDictionary<Guid, string> giftNames)
    {
        return new DashboardPaymentActivityDto
        {
            Id = payment.Id,
            GiftId = payment.GiftId,
            GiftName = GetGiftName(giftNames, payment.GiftId),
            ContributorName = payment.ContributorName,
            Method = payment.Method,
            Amount = payment.Amount,
            Installments = payment.Installments,
            Status = payment.Status,
            StatusDetail = payment.StatusDetail ?? string.Empty,
            OrderId = payment.OrderId,
            MpOrderId = payment.MpOrderId ?? string.Empty,
            MpPaymentId = payment.MpPaymentId ?? string.Empty,
            ContributionCreated = payment.ContributionCreated,
            CreatedAtUtc = payment.CreatedAt,
            UpdatedAtUtc = payment.UpdatedAt
        };
    }

    private static DashboardContributionActivityDto ToContributionActivityDto(
        Contribution contribution,
        IReadOnlyDictionary<Guid, string> giftNames)
    {
        return new DashboardContributionActivityDto
        {
            Id = contribution.Id,
            GiftId = contribution.GiftId,
            GiftName = GetGiftName(giftNames, contribution.GiftId),
            ContributorName = contribution.ContributorName,
            Message = contribution.Message,
            Amount = contribution.Amount,
            PaymentMethod = contribution.PaymentMethod,
            Status = contribution.Status,
            PaidAtUtc = contribution.PaidAt
        };
    }

    private static DashboardApiRequestActivityDto ToRequestActivityDto(ApiRequestLog requestLog)
    {
        return new DashboardApiRequestActivityDto
        {
            Id = requestLog.Id,
            Method = requestLog.Method,
            Path = requestLog.Path,
            StatusCode = requestLog.StatusCode,
            IsSuccess = requestLog.IsSuccess,
            IsAuthenticated = requestLog.IsAuthenticated,
            UserRole = requestLog.UserRole,
            DurationMilliseconds = requestLog.DurationMilliseconds,
            CorrelationId = requestLog.CorrelationId,
            ExceptionType = requestLog.ExceptionType,
            ExceptionMessage = requestLog.ExceptionMessage,
            StartedAtUtc = requestLog.StartedAtUtc
        };
    }

    private static int CountUniqueContributors(IEnumerable<Contribution> contributions)
    {
        return contributions
            .Select(x => x.ContributorName.Trim().ToLowerInvariant())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .Count();
    }

    private static bool IsApprovedPayment(string status)
        => string.Equals(status, "approved", StringComparison.OrdinalIgnoreCase);

    private static bool IsPendingPayment(string status)
    {
        string normalized = NormalizeStatus(status);
        return normalized is "pending" or "in_process" or "in_mediation";
    }

    private static bool IsFailedPayment(string status)
    {
        string normalized = NormalizeStatus(status);
        return normalized is "error" or "rejected" or "cancelled" or "canceled" or "refunded" or "charged_back";
    }

    private static bool IsCardPayment(string method)
    {
        string normalized = NormalizeMethod(method);
        return normalized is "credit_card" or "debit_card" or "card";
    }

    private static decimal CalculatePercent(decimal current, decimal total)
    {
        if (total <= 0)
        {
            return 0;
        }

        return Math.Round(current / total * 100, 2);
    }

    private static decimal CalculateAverageDuration(IEnumerable<ApiRequestLog> requestLogs)
    {
        List<ApiRequestLog> items = requestLogs.ToList();
        return items.Count == 0 ? 0 : Math.Round((decimal)items.Average(x => x.DurationMilliseconds), 2);
    }

    private static string GetStatusGroup(int statusCode)
    {
        if (statusCode <= 0)
        {
            return "unknown";
        }

        return $"{statusCode / 100}xx";
    }

    private static string GetGiftName(IReadOnlyDictionary<Guid, string> giftNames, Guid giftId)
        => giftNames.TryGetValue(giftId, out string giftName) ? giftName : string.Empty;

    private static string NormalizeStatus(string value)
        => string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim().ToLowerInvariant();

    private static string NormalizeMethod(string value)
        => string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim().ToLowerInvariant();

    private static string NormalizeCategory(string value)
        => string.IsNullOrWhiteSpace(value) ? "Sem categoria" : value.Trim();
}
