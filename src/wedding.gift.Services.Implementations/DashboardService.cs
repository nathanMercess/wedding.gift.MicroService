using Microsoft.Extensions.Caching.Memory;
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
    IMemoryCache cache,
    IApplicationCacheService cacheService,
    ILogger<DashboardService> logger) : IDashboardService
{
    private const int SlowRequestThresholdMilliseconds = 1000;
    private const int PendingPaymentWarningMinutes = 30;
    private const int RecentFailureHours = 24;
    private const int MaxRequestLogsToLoad = 5000;
    private static readonly TimeSpan DashboardCacheDuration = TimeSpan.FromSeconds(15);

    public async Task<DashboardResponseDto> GetAsync(DashboardQueryDto query, CancellationToken cancellationToken)
    {
        DashboardData data = await LoadDashboardDataAsync(query, cancellationToken);
        DashboardOverviewResponseDto overview = BuildOverview(data);
        DashboardChartsDto charts = BuildCharts(data);

        return new DashboardResponseDto
        {
            GeneratedAtUtc = overview.GeneratedAtUtc,
            Period = overview.Period,
            Overview = overview.Overview,
            Gifts = overview.Gifts,
            Contributions = overview.Contributions,
            Payments = overview.Payments,
            Messages = overview.Messages,
            Requests = overview.Requests,
            Monitoring = overview.Monitoring,
            ActionCenter = BuildActionCenter(data),
            Revenue = BuildRevenue(data),
            PaymentHealth = BuildPaymentHealth(data),
            GiftInsights = BuildGiftInsights(data),
            ApiHealth = BuildApiHealth(data),
            ContributionsByDay = charts.ContributionsByDay,
            PaymentsByStatus = charts.PaymentsByStatus,
            PaymentsByMethod = charts.PaymentsByMethod,
            GiftsByCategory = charts.GiftsByCategory,
            RequestsByStatus = charts.RequestsByStatus,
            RequestsByPath = charts.RequestsByPath,
            TopGiftsByRaised = BuildTopGiftsByRaised(data),
            RecentMessages = BuildRecentMessages(data),
            RecentRequests = BuildRecentRequests(data),
            RecentPayments = BuildRecentPayments(data),
            RecentFailedPayments = BuildRecentFailedPayments(data),
            RecentContributions = BuildRecentContributions(data),
            ActivityFeed = BuildActivityFeed(data)
        };
    }

    public async Task<DashboardOverviewResponseDto> GetOverviewAsync(DashboardQueryDto query, CancellationToken cancellationToken)
        => BuildOverview(await LoadDashboardDataAsync(query, cancellationToken));

    public async Task<DashboardChartsDto> GetChartsAsync(DashboardQueryDto query, CancellationToken cancellationToken)
        => BuildCharts(await LoadDashboardDataAsync(query, cancellationToken));

    public async Task<DashboardActionCenterDto> GetActionCenterAsync(DashboardQueryDto query, CancellationToken cancellationToken)
        => BuildActionCenter(await LoadDashboardDataAsync(query, cancellationToken));

    public async Task<DashboardRevenueDto> GetRevenueAsync(DashboardQueryDto query, CancellationToken cancellationToken)
        => BuildRevenue(await LoadDashboardDataAsync(query, cancellationToken));

    public async Task<DashboardPaymentHealthDto> GetPaymentHealthAsync(DashboardQueryDto query, CancellationToken cancellationToken)
        => BuildPaymentHealth(await LoadDashboardDataAsync(query, cancellationToken));

    public async Task<DashboardGiftInsightsDto> GetGiftInsightsAsync(DashboardQueryDto query, CancellationToken cancellationToken)
        => BuildGiftInsights(await LoadDashboardDataAsync(query, cancellationToken));

    public async Task<DashboardApiHealthDto> GetApiHealthAsync(DashboardQueryDto query, CancellationToken cancellationToken)
        => BuildApiHealth(await LoadDashboardDataAsync(query, cancellationToken));

    public async Task<List<DashboardActivityFeedItemDto>> GetActivityFeedAsync(DashboardQueryDto query, CancellationToken cancellationToken)
        => BuildActivityFeed(await LoadDashboardDataAsync(query, cancellationToken));

    private async Task<DashboardData> LoadDashboardDataAsync(DashboardQueryDto query, CancellationToken cancellationToken)
    {
        if (query.Days < 1 || query.Days > 365)
            throw new BadRequestException(ErrorCodes.INVALID_DASHBOARD_DAYS);

        if (query.RecentItems < 1 || query.RecentItems > 50)
            throw new BadRequestException(ErrorCodes.INVALID_DASHBOARD_RECENT_ITEMS);

        string cacheKey = $"dashboard:data:{cacheService.CurrentVersion}:{query.Days}:{query.RecentItems}";
        DashboardData? cached = await cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = DashboardCacheDuration;

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
            List<DashboardMessageDto> contributionMessages = BuildContributionMessages(contributions, giftNames);
            List<DashboardMessageDto> paymentMessages = BuildPaymentIntentMessages(payments, giftNames);
            List<DashboardMessageDto> allMessages = contributionMessages
                .Concat(paymentMessages)
                .OrderByDescending(x => x.CreatedAtUtc)
                .ToList();

            return new DashboardData
            {
                Query = query,
                Now = now,
                FromUtc = fromUtc,
                Gifts = gifts,
                Contributions = contributions,
                Payments = payments,
                RequestLogs = requestLogs,
                GiftNames = giftNames,
                GiftFunding = giftFunding,
                PaidContributions = paidContributions,
                PendingContributions = pendingContributions,
                CancelledContributions = cancelledContributions,
                PeriodPaidContributions = periodPaidContributions,
                ApprovedPayments = approvedPayments,
                PendingPayments = pendingPayments,
                FailedPayments = failedPayments,
                ContributionMessages = contributionMessages,
                PaymentMessages = paymentMessages,
                AllMessages = allMessages
            };
        });

        return cached!;
    }

    private async Task<List<ApiRequestLog>> GetRequestLogsAsync(
        DateTime fromUtc,
        DateTime now,
        CancellationToken cancellationToken)
    {
        try
        {
            return (await apiRequestLogRepository.GetByStartedAtRangeAsync(fromUtc, now, MaxRequestLogsToLoad, cancellationToken)).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao carregar logs de request para o dashboard.");
            return [];
        }
    }

    private static DashboardOverviewResponseDto BuildOverview(DashboardData data)
    {
        int categorizedPaymentCount = data.ApprovedPayments.Count + data.PendingPayments.Count + data.FailedPayments.Count;
        int approvedWithoutContribution = data.ApprovedPayments.Count(x => !x.ContributionCreated);
        int serverErrorRequests = data.RequestLogs.Count(x => x.StatusCode >= 500);
        int slowRequests = data.RequestLogs.Count(x => x.DurationMilliseconds >= SlowRequestThresholdMilliseconds);

        return new DashboardOverviewResponseDto
        {
            GeneratedAtUtc = data.Now,
            Period = new DashboardPeriodDto
            {
                FromUtc = data.FromUtc,
                ToUtc = data.Now,
                Days = data.Query.Days
            },
            Overview = new DashboardOverviewDto
            {
                TotalRaised = data.PaidContributions.Sum(x => x.NetAmount),
                TotalGoal = data.Gifts.Sum(x => x.Total),
                FundingPercent = CalculatePercent(data.PaidContributions.Sum(x => x.NetAmount), data.Gifts.Sum(x => x.Total)),
                TotalGifts = data.Gifts.Count,
                FullyFundedGifts = data.GiftFunding.Count(x => x.FullyFunded),
                PaidContributions = data.PaidContributions.Count,
                UniqueContributors = CountUniqueContributors(data.PaidContributions),
                ApprovedPayments = data.ApprovedPayments.Count,
                FailedPayments = data.FailedPayments.Count
            },
            Gifts = BuildGiftSummary(data.Gifts, data.GiftFunding),
            Contributions = new DashboardContributionSummaryDto
            {
                Total = data.Contributions.Count,
                Paid = data.PaidContributions.Count,
                Pending = data.PendingContributions.Count,
                Cancelled = data.CancelledContributions.Count,
                PaidAmount = data.PaidContributions.Sum(x => x.NetAmount),
                PendingAmount = data.PendingContributions.Sum(x => x.Amount),
                CancelledAmount = data.CancelledContributions.Sum(x => x.Amount),
                AveragePaidAmount = data.PaidContributions.Count == 0 ? 0 : Math.Round(data.PaidContributions.Average(x => x.NetAmount), 2),
                UniqueContributors = CountUniqueContributors(data.PaidContributions),
                MessagesCount = data.ContributionMessages.Count,
                PeriodPaidCount = data.PeriodPaidContributions.Count,
                PeriodPaidAmount = data.PeriodPaidContributions.Sum(x => x.NetAmount)
            },
            Payments = new DashboardPaymentSummaryDto
            {
                Total = data.Payments.Count,
                Approved = data.ApprovedPayments.Count,
                Pending = data.PendingPayments.Count,
                Failed = data.FailedPayments.Count,
                Other = data.Payments.Count - categorizedPaymentCount,
                Pix = data.Payments.Count(x => string.Equals(x.Method, "pix", StringComparison.OrdinalIgnoreCase)),
                Card = data.Payments.Count(x => IsCardPayment(x.Method)),
                ApprovedWithoutContribution = approvedWithoutContribution,
                ApprovedAmount = data.ApprovedPayments.Sum(x => x.Amount),
                PendingAmount = data.PendingPayments.Sum(x => x.Amount),
                FailedAmount = data.FailedPayments.Sum(x => x.Amount),
                SuccessRate = CalculatePercent(data.ApprovedPayments.Count, data.Payments.Count),
                FailureRate = CalculatePercent(data.FailedPayments.Count, data.Payments.Count)
            },
            Messages = new DashboardMessageSummaryDto
            {
                Total = data.AllMessages.Count,
                ContributionMessages = data.ContributionMessages.Count,
                PaymentIntentMessages = data.PaymentMessages.Count,
                LatestMessageAtUtc = data.AllMessages.FirstOrDefault()?.CreatedAtUtc
            },
            Requests = BuildRequestSummary(data.RequestLogs),
            Monitoring = new DashboardMonitoringDto
            {
                DatabaseStatus = "Online",
                ApplicationLogsStatus = "ApiRequestLogs ativo",
                MetricsStatus = "Pendente de validacao",
                PendingPayments = data.PendingPayments.Count,
                PendingPixPayments = data.PendingPayments.Count(x => string.Equals(x.Method, "pix", StringComparison.OrdinalIgnoreCase)),
                FailedPayments = data.FailedPayments.Count,
                ApprovedPaymentsWithoutContribution = approvedWithoutContribution,
                ServerErrorRequests = serverErrorRequests,
                SlowRequests = slowRequests,
                AverageRequestDurationMilliseconds = CalculateAverageDuration(data.RequestLogs),
                LastPaymentAtUtc = data.Payments.OrderByDescending(x => x.CreatedAt).FirstOrDefault()?.CreatedAt,
                LastContributionAtUtc = data.Contributions.OrderByDescending(x => x.PaidAt).FirstOrDefault()?.PaidAt,
                LastRequestAtUtc = data.RequestLogs.OrderByDescending(x => x.StartedAtUtc).FirstOrDefault()?.StartedAtUtc,
                Notes =
                [
                    "Falhas e sucessos representam pagamentos persistidos no banco.",
                    "Requests da API sao persistidos em ApiRequestLogs sem body, tokens, cookies ou headers sensiveis.",
                    "Metricas APM, e-mail e traces distribuidos: Pendente de validacao."
                ]
            }
        };
    }

    private static DashboardChartsDto BuildCharts(DashboardData data)
    {
        return new DashboardChartsDto
        {
            ContributionsByDay = BuildContributionsByDay(data.PeriodPaidContributions, data.FromUtc, data.Query.Days),
            PaymentsByStatus = BuildPaymentsByStatus(data.Payments),
            PaymentsByMethod = BuildPaymentsByMethod(data.Payments),
            GiftsByCategory = BuildGiftsByCategory(data.GiftFunding),
            RequestsByStatus = BuildRequestsByStatus(data.RequestLogs),
            RequestsByPath = BuildRequestsByPath(data.RequestLogs)
                .Take(data.Query.RecentItems)
                .ToList()
        };
    }

    private static DashboardActionCenterDto BuildActionCenter(DashboardData data)
    {
        List<DashboardActionItemDto> items = [];
        List<Payment> approvedWithoutContribution = data.ApprovedPayments.Where(x => !x.ContributionCreated).ToList();
        List<Payment> oldPendingPayments = data.PendingPayments.Where(x => x.CreatedAt <= data.Now.AddMinutes(-PendingPaymentWarningMinutes)).ToList();
        List<Payment> recentFailedPayments = data.FailedPayments.Where(x => GetPaymentActivityAt(x) >= data.Now.AddHours(-RecentFailureHours)).ToList();
        List<ApiRequestLog> serverErrors = data.RequestLogs.Where(x => x.StatusCode >= 500).ToList();
        List<ApiRequestLog> slowRequests = data.RequestLogs.Where(x => x.DurationMilliseconds >= SlowRequestThresholdMilliseconds).ToList();
        List<DashboardGiftFundingDto> fullyFundedButAvailable = data.GiftFunding.Where(x => x.FullyFunded && x.Available).ToList();
        List<DashboardGiftFundingDto> giftsWithoutContribution = data.GiftFunding.Where(x => x.PaidContributions == 0 && x.Available).ToList();
        List<DashboardGiftFundingDto> overfundedGifts = data.GiftFunding.Where(x => x.Raised > x.Total && x.Total > 0).ToList();
        List<ApiRequestLog> emailFailures = data.RequestLogs.Where(ContainsEmailFailure).ToList();

        AddActionItem(items, approvedWithoutContribution.Count, "critical", "Pagamentos aprovados sem contribuicao", "O Mercado Pago aprovou pagamentos, mas a contribuicao ainda nao foi criada.", FormatCount(approvedWithoutContribution.Count, "pagamento", "pagamentos"), "Reprocessar contribuicoes", "payments", approvedWithoutContribution.OrderByDescending(GetPaymentActivityAt).FirstOrDefault() is Payment payment ? GetPaymentActivityAt(payment) : null);
        AddActionItem(items, oldPendingPayments.Count, "warning", "Pagamentos pendentes antigos", "Pagamentos aguardam confirmacao ha mais de 30 minutos.", FormatCount(oldPendingPayments.Count, "pagamento", "pagamentos"), "Ver pendentes", "payments", oldPendingPayments.OrderByDescending(GetPaymentActivityAt).FirstOrDefault() is Payment oldPayment ? GetPaymentActivityAt(oldPayment) : null);
        AddActionItem(items, recentFailedPayments.Count, "critical", "Falhas recentes de pagamento", "Pagamentos falharam nas ultimas 24 horas e podem indicar problema no provedor.", FormatCount(recentFailedPayments.Count, "falha", "falhas"), "Investigar falhas", "payments", recentFailedPayments.OrderByDescending(GetPaymentActivityAt).FirstOrDefault() is Payment failedPayment ? GetPaymentActivityAt(failedPayment) : null);
        AddActionItem(items, serverErrors.Count, "critical", "Requests 5xx na API", "A API registrou erros de servidor no periodo selecionado.", FormatCount(serverErrors.Count, "erro", "erros"), "Abrir erros", "api", serverErrors.OrderByDescending(x => x.StartedAtUtc).FirstOrDefault()?.StartedAtUtc);
        AddActionItem(items, slowRequests.Count, "warning", "Endpoints lentos", "Requests ultrapassaram 1 segundo de duracao.", FormatCount(slowRequests.Count, "request", "requests"), "Ver endpoints", "api", slowRequests.OrderByDescending(x => x.DurationMilliseconds).FirstOrDefault()?.StartedAtUtc);
        AddActionItem(items, fullyFundedButAvailable.Count, "critical", "Presentes completos ainda disponiveis", "Presentes ja financiados continuam marcados como disponiveis.", FormatCount(fullyFundedButAvailable.Count, "presente", "presentes"), "Corrigir disponibilidade", "gifts", null);
        AddActionItem(items, giftsWithoutContribution.Count, "warning", "Presentes sem contribuicao", "Presentes disponiveis ainda nao receberam contribuicoes pagas.", FormatCount(giftsWithoutContribution.Count, "presente", "presentes"), "Revisar lista", "gifts", null);
        AddActionItem(items, overfundedGifts.Count, "critical", "Presentes acima do valor total", "Presentes arrecadaram mais do que o total cadastrado.", FormatCount(overfundedGifts.Count, "presente", "presentes"), "Auditar valores", "gifts", null);
        AddActionItem(items, emailFailures.Count, "critical", "Falhas de e-mail nos logs", "Requests recentes registraram excecoes relacionadas ao envio de e-mail.", FormatCount(emailFailures.Count, "falha", "falhas"), "Ver erros de e-mail", "email", emailFailures.OrderByDescending(x => x.StartedAtUtc).FirstOrDefault()?.StartedAtUtc);

        List<DashboardActionItemDto> orderedItems = items
            .OrderBy(x => GetSeverityRank(x.Severity))
            .ThenByDescending(x => x.CreatedAtUtc ?? DateTime.MinValue)
            .ThenBy(x => x.Title)
            .ToList();

        return new DashboardActionCenterDto
        {
            HealthStatus = orderedItems.Any(x => x.Severity == "critical") ? "critical" : orderedItems.Any(x => x.Severity == "warning") ? "warning" : "healthy",
            CriticalCount = orderedItems.Count(x => x.Severity == "critical"),
            WarningCount = orderedItems.Count(x => x.Severity == "warning"),
            Items = orderedItems
        };
    }

    private static DashboardRevenueDto BuildRevenue(DashboardData data)
    {
        List<DashboardTimeSeriesPointDto> contributionsByDay = BuildContributionsByDay(data.PeriodPaidContributions, data.FromUtc, data.Query.Days);
        DashboardTimeSeriesPointDto? bestDay = contributionsByDay
            .OrderByDescending(x => x.Amount)
            .ThenByDescending(x => x.Count)
            .FirstOrDefault();
        decimal totalRaised = data.PaidContributions.Sum(x => x.NetAmount);
        decimal totalGoal = data.Gifts.Sum(x => x.Total);
        decimal periodRaised = data.PeriodPaidContributions.Sum(x => x.NetAmount);

        return new DashboardRevenueDto
        {
            TotalRaised = totalRaised,
            PeriodRaised = periodRaised,
            RemainingAmount = Math.Max(totalGoal - totalRaised, 0),
            AverageTicket = data.PaidContributions.Count == 0 ? 0 : Math.Round(data.PaidContributions.Average(x => x.NetAmount), 2),
            LargestContribution = data.PaidContributions.Count == 0 ? 0 : data.PaidContributions.Max(x => x.NetAmount),
            PeriodPaidCount = data.PeriodPaidContributions.Count,
            FundingPercent = CalculatePercent(totalRaised, totalGoal),
            DailyAverage = data.Query.Days == 0 ? 0 : Math.Round(periodRaised / data.Query.Days, 2),
            BestDayAmount = bestDay?.Amount ?? 0,
            BestDayUtc = bestDay?.Amount > 0 ? bestDay.DateUtc : null
        };
    }

    private static DashboardPaymentHealthDto BuildPaymentHealth(DashboardData data)
    {
        List<Payment> oldPendingPayments = data.PendingPayments.Where(x => x.CreatedAt <= data.Now.AddMinutes(-PendingPaymentWarningMinutes)).ToList();
        List<Payment> recentFailedPayments = data.FailedPayments.Where(x => GetPaymentActivityAt(x) >= data.Now.AddHours(-RecentFailureHours)).ToList();

        return new DashboardPaymentHealthDto
        {
            ApprovalRate = CalculatePercent(data.ApprovedPayments.Count, data.Payments.Count),
            FailureRate = CalculatePercent(data.FailedPayments.Count, data.Payments.Count),
            PendingCount = data.PendingPayments.Count,
            PendingAmount = data.PendingPayments.Sum(x => x.Amount),
            PendingOlderThan30Minutes = oldPendingPayments.Count,
            ApprovedWithoutContribution = data.ApprovedPayments.Count(x => !x.ContributionCreated),
            FailedLast24Hours = recentFailedPayments.Count,
            TopFailureReasons = data.FailedPayments
                .GroupBy(GetFailureReason)
                .Select(x => new DashboardPaymentFailureReasonDto
                {
                    StatusDetail = x.Key,
                    Count = x.Count()
                })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.StatusDetail)
                .Take(data.Query.RecentItems)
                .ToList(),
            LastFailureAtUtc = data.FailedPayments.Count == 0 ? null : data.FailedPayments.Max(GetPaymentActivityAt)
        };
    }

    private static DashboardGiftInsightsDto BuildGiftInsights(DashboardData data)
    {
        return new DashboardGiftInsightsDto
        {
            Total = data.GiftFunding.Count,
            FullyFunded = data.GiftFunding.Count(x => x.FullyFunded),
            Available = data.GiftFunding.Count(x => x.Available),
            FullyFundedButAvailable = data.GiftFunding.Count(x => x.FullyFunded && x.Available),
            WithoutContributions = data.GiftFunding.Count(x => x.PaidContributions == 0),
            Overfunded = data.GiftFunding.Count(x => x.Raised > x.Total && x.Total > 0),
            TopRemainingGifts = data.GiftFunding
                .Where(x => x.Remaining > 0)
                .OrderByDescending(x => x.Remaining)
                .ThenBy(x => x.GiftName)
                .Take(data.Query.RecentItems)
                .ToList(),
            TopRaisedGifts = BuildTopGiftsByRaised(data),
            StalledGifts = data.GiftFunding
                .Where(x => x.PaidContributions == 0 && x.Available)
                .OrderByDescending(x => x.Total)
                .ThenBy(x => x.GiftName)
                .Take(data.Query.RecentItems)
                .ToList()
        };
    }

    private static DashboardApiHealthDto BuildApiHealth(DashboardData data)
    {
        List<DashboardApiEndpointHealthDto> endpoints = BuildApiEndpointHealth(data.RequestLogs);
        int successful = data.RequestLogs.Count(x => x.StatusCode < 400);

        return new DashboardApiHealthDto
        {
            SuccessRate = CalculatePercent(successful, data.RequestLogs.Count),
            ServerErrors = data.RequestLogs.Count(x => x.StatusCode >= 500),
            ClientErrors = data.RequestLogs.Count(x => x.StatusCode >= 400 && x.StatusCode < 500),
            SlowRequests = data.RequestLogs.Count(x => x.DurationMilliseconds >= SlowRequestThresholdMilliseconds),
            AverageDurationMilliseconds = CalculateAverageDuration(data.RequestLogs),
            P95DurationMilliseconds = CalculatePercentileDuration(data.RequestLogs.Select(x => x.DurationMilliseconds), 0.95m),
            SlowestEndpoints = endpoints
                .Where(x => x.Count > 0)
                .OrderByDescending(x => x.P95DurationMilliseconds)
                .ThenByDescending(x => x.MaxDurationMilliseconds)
                .ThenBy(x => x.Path)
                .Take(data.Query.RecentItems)
                .ToList(),
            TopErrorEndpoints = endpoints
                .Where(x => x.ServerErrors + x.ClientErrors > 0)
                .OrderByDescending(x => x.ServerErrors)
                .ThenByDescending(x => x.ClientErrors)
                .ThenByDescending(x => x.Count)
                .ThenBy(x => x.Path)
                .Take(data.Query.RecentItems)
                .ToList(),
            LastServerErrorAtUtc = data.RequestLogs
                .Where(x => x.StatusCode >= 500)
                .OrderByDescending(x => x.StartedAtUtc)
                .FirstOrDefault()
                ?.StartedAtUtc
        };
    }

    private static List<DashboardActivityFeedItemDto> BuildActivityFeed(DashboardData data)
    {
        List<DashboardActivityFeedItemDto> items = [];

        items.AddRange(data.Contributions.Select(x => new DashboardActivityFeedItemDto
        {
            Type = "contribution",
            Severity = string.Equals(x.Status, ContributionStatus.Paid, StringComparison.OrdinalIgnoreCase) ? "success" : "warning",
            Title = string.Equals(x.Status, ContributionStatus.Paid, StringComparison.OrdinalIgnoreCase) ? "Contribuicao recebida" : "Contribuicao pendente",
            Description = $"{x.ContributorName} - {GetGiftName(data.GiftNames, x.GiftId)}",
            Amount = x.Amount,
            Status = x.Status,
            OccurredAtUtc = x.PaidAt
        }));

        items.AddRange(data.Payments.Select(x => new DashboardActivityFeedItemDto
        {
            Type = "payment",
            Severity = GetPaymentSeverity(x),
            Title = GetPaymentTitle(x),
            Description = $"{x.ContributorName} - {GetGiftName(data.GiftNames, x.GiftId)} - {NormalizeMethod(x.Method)}",
            Amount = x.Amount,
            Status = x.Status,
            OccurredAtUtc = GetPaymentActivityAt(x)
        }));

        items.AddRange(data.AllMessages.Select(x => new DashboardActivityFeedItemDto
        {
            Type = "message",
            Severity = "info",
            Title = "Mensagem recebida",
            Description = $"{x.ContributorName} - {GetGiftName(data.GiftNames, x.GiftId)}: {TrimText(x.Message, 120)}",
            Amount = x.Amount,
            Status = x.Status,
            OccurredAtUtc = x.CreatedAtUtc
        }));

        items.AddRange(data.RequestLogs
            .Where(x => x.StatusCode >= 500)
            .Select(x => new DashboardActivityFeedItemDto
            {
                Type = ContainsEmailFailure(x) ? "email" : "api",
                Severity = "critical",
                Title = ContainsEmailFailure(x) ? "Falha de e-mail" : $"Erro {x.StatusCode} na API",
                Description = $"{x.Method} {x.Path} - {TrimText(GetRequestErrorDescription(x), 140)}",
                Status = x.StatusCode.ToString(),
                CorrelationId = x.CorrelationId,
                OccurredAtUtc = x.StartedAtUtc
            }));

        return items
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(data.Query.RecentItems)
            .ToList();
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
            SlowRequests = requestLogs.Count(x => x.DurationMilliseconds >= SlowRequestThresholdMilliseconds),
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
            Available = gifts.Count(x => !x.FullyFunded),
            Unavailable = gifts.Count(x => x.FullyFunded),
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
                decimal raised = paidContributions.Sum(x => x.NetAmount);
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
                    Available = !gift.FullyFunded,
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
                Amount = x.Sum(c => c.NetAmount)
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

    private static List<DashboardApiEndpointHealthDto> BuildApiEndpointHealth(IEnumerable<ApiRequestLog> requestLogs)
    {
        return requestLogs
            .GroupBy(x => (x.Method, x.Path))
            .Select(x => new DashboardApiEndpointHealthDto
            {
                Method = x.Key.Method,
                Path = x.Key.Path,
                Count = x.Count(),
                ServerErrors = x.Count(r => r.StatusCode >= 500),
                ClientErrors = x.Count(r => r.StatusCode >= 400 && r.StatusCode < 500),
                SlowRequests = x.Count(r => r.DurationMilliseconds >= SlowRequestThresholdMilliseconds),
                AverageDurationMilliseconds = CalculateAverageDuration(x),
                P95DurationMilliseconds = CalculatePercentileDuration(x.Select(r => r.DurationMilliseconds), 0.95m),
                MaxDurationMilliseconds = x.Max(r => r.DurationMilliseconds)
            })
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
                Message = x.Message.Trim(),
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

    private static List<DashboardGiftFundingDto> BuildTopGiftsByRaised(DashboardData data)
    {
        return data.GiftFunding
            .OrderByDescending(x => x.Raised)
            .ThenBy(x => x.GiftName)
            .Take(data.Query.RecentItems)
            .ToList();
    }

    private static List<DashboardMessageDto> BuildRecentMessages(DashboardData data)
        => data.AllMessages.Take(data.Query.RecentItems).ToList();

    private static List<DashboardApiRequestActivityDto> BuildRecentRequests(DashboardData data)
    {
        return data.RequestLogs
            .OrderByDescending(x => x.StartedAtUtc)
            .Take(data.Query.RecentItems)
            .Select(ToRequestActivityDto)
            .ToList();
    }

    private static List<DashboardPaymentActivityDto> BuildRecentPayments(DashboardData data)
    {
        return data.Payments
            .OrderByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.CreatedAt)
            .Take(data.Query.RecentItems)
            .Select(x => ToPaymentActivityDto(x, data.GiftNames))
            .ToList();
    }

    private static List<DashboardPaymentActivityDto> BuildRecentFailedPayments(DashboardData data)
    {
        return data.FailedPayments
            .OrderByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.CreatedAt)
            .Take(data.Query.RecentItems)
            .Select(x => ToPaymentActivityDto(x, data.GiftNames))
            .ToList();
    }

    private static List<DashboardContributionActivityDto> BuildRecentContributions(DashboardData data)
    {
        return data.Contributions
            .OrderByDescending(x => x.PaidAt)
            .Take(data.Query.RecentItems)
            .Select(x => ToContributionActivityDto(x, data.GiftNames))
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

    private static void AddActionItem(
        ICollection<DashboardActionItemDto> items,
        int count,
        string severity,
        string title,
        string description,
        string metric,
        string actionLabel,
        string category,
        DateTime? createdAtUtc)
    {
        if (count <= 0)
            return;

        items.Add(new DashboardActionItemDto
        {
            Severity = severity,
            Title = title,
            Description = description,
            Metric = metric,
            ActionLabel = actionLabel,
            Category = category,
            CreatedAtUtc = createdAtUtc
        });
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

    private static bool ContainsEmailFailure(ApiRequestLog requestLog)
        => ContainsEmailText(requestLog.ExceptionType) || ContainsEmailText(requestLog.ExceptionMessage) || ContainsEmailText(requestLog.Path);

    private static bool ContainsEmailText(string value)
        => value.Contains("email", StringComparison.OrdinalIgnoreCase) || value.Contains("e-mail", StringComparison.OrdinalIgnoreCase);

    private static decimal CalculatePercent(decimal current, decimal total)
    {
        if (total <= 0)
            return 0;

        return Math.Round(current / total * 100, 2);
    }

    private static decimal CalculateAverageDuration(IEnumerable<ApiRequestLog> requestLogs)
    {
        List<ApiRequestLog> items = requestLogs.ToList();
        return items.Count == 0 ? 0 : Math.Round((decimal)items.Average(x => x.DurationMilliseconds), 2);
    }

    private static decimal CalculatePercentileDuration(IEnumerable<long> durations, decimal percentile)
    {
        List<long> orderedDurations = durations.OrderBy(x => x).ToList();

        if (orderedDurations.Count == 0)
            return 0;

        int index = Math.Max((int)Math.Ceiling(orderedDurations.Count * percentile) - 1, 0);
        return orderedDurations[index];
    }

    private static DateTime GetPaymentActivityAt(Payment payment)
        => payment.UpdatedAt > payment.CreatedAt ? payment.UpdatedAt : payment.CreatedAt;

    private static int GetSeverityRank(string severity)
    {
        if (severity == "critical")
            return 0;

        if (severity == "warning")
            return 1;

        return 2;
    }

    private static string GetStatusGroup(int statusCode)
    {
        if (statusCode <= 0)
            return "unknown";

        return $"{statusCode / 100}xx";
    }

    private static string GetPaymentSeverity(Payment payment)
    {
        if (IsApprovedPayment(payment.Status) && !payment.ContributionCreated)
            return "critical";

        if (IsFailedPayment(payment.Status))
            return "critical";

        if (IsPendingPayment(payment.Status))
            return "warning";

        if (IsApprovedPayment(payment.Status))
            return "success";

        return "info";
    }

    private static string GetPaymentTitle(Payment payment)
    {
        if (IsApprovedPayment(payment.Status) && !payment.ContributionCreated)
            return "Pagamento aprovado sem contribuicao";

        if (IsFailedPayment(payment.Status))
            return "Pagamento com falha";

        if (IsPendingPayment(payment.Status))
            return "Pagamento pendente";

        if (IsApprovedPayment(payment.Status))
            return "Pagamento aprovado";

        return "Pagamento atualizado";
    }

    private static string GetFailureReason(Payment payment)
    {
        if (!string.IsNullOrWhiteSpace(payment.StatusDetail))
            return NormalizeStatus(payment.StatusDetail);

        return NormalizeStatus(payment.Status);
    }

    private static string GetRequestErrorDescription(ApiRequestLog requestLog)
    {
        if (!string.IsNullOrWhiteSpace(requestLog.ExceptionMessage))
            return requestLog.ExceptionMessage;

        if (!string.IsNullOrWhiteSpace(requestLog.ExceptionType))
            return requestLog.ExceptionType;

        return "Request finalizado com erro de servidor.";
    }

    private static string FormatCount(int count, string singular, string plural)
        => $"{count} {(count == 1 ? singular : plural)}";

    private static string TrimText(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : $"{trimmed[..maxLength]}...";
    }

    private static string GetGiftName(IReadOnlyDictionary<Guid, string> giftNames, Guid giftId)
        => giftNames.TryGetValue(giftId, out string giftName) ? giftName : string.Empty;

    private static string NormalizeStatus(string value)
        => string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim().ToLowerInvariant();

    private static string NormalizeMethod(string value)
        => string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim().ToLowerInvariant();

    private static string NormalizeCategory(string value)
        => string.IsNullOrWhiteSpace(value) ? "Sem categoria" : value.Trim();

    private sealed class DashboardData
    {
        public DashboardQueryDto Query { get; set; } = new();
        public DateTime Now { get; set; }
        public DateTime FromUtc { get; set; }
        public List<Gift> Gifts { get; set; } = [];
        public List<Contribution> Contributions { get; set; } = [];
        public List<Payment> Payments { get; set; } = [];
        public List<ApiRequestLog> RequestLogs { get; set; } = [];
        public Dictionary<Guid, string> GiftNames { get; set; } = [];
        public List<DashboardGiftFundingDto> GiftFunding { get; set; } = [];
        public List<Contribution> PaidContributions { get; set; } = [];
        public List<Contribution> PendingContributions { get; set; } = [];
        public List<Contribution> CancelledContributions { get; set; } = [];
        public List<Contribution> PeriodPaidContributions { get; set; } = [];
        public List<Payment> ApprovedPayments { get; set; } = [];
        public List<Payment> PendingPayments { get; set; } = [];
        public List<Payment> FailedPayments { get; set; } = [];
        public List<DashboardMessageDto> ContributionMessages { get; set; } = [];
        public List<DashboardMessageDto> PaymentMessages { get; set; } = [];
        public List<DashboardMessageDto> AllMessages { get; set; } = [];
    }
}
