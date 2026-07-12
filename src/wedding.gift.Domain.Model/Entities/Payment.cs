namespace wedding.gift.Domain.Model.Entities;

public sealed class Payment
{
    private Payment()
    {
    }

    public Guid Id { get; private set; }
    public Guid CoupleId { get; private set; }
    public Guid GiftId { get; private set; }
    public string GiftName { get; private set; } = string.Empty;
    public string ContributorName { get; private set; } = string.Empty;
    public string? Message { get; private set; }
    public string PayerEmail { get; private set; } = string.Empty;
    public string PayerDocType { get; private set; } = string.Empty;
    public string PayerDocNumber { get; private set; } = string.Empty;
    public Guid? ContributionId { get; private set; }
    public Contribution? Contribution { get; private set; }
    public bool ContributionCreated { get; private set; }
    public string OrderId { get; private set; } = string.Empty;
    public string Method { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public decimal RefundedAmount { get; private set; }
    public int Installments { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public string? StatusDetail { get; private set; }
    public string? Nsu { get; private set; }
    public string? MpOrderId { get; private set; }
    public string? MpPaymentId { get; private set; }
    public string? PixQrCode { get; private set; }
    public string? QrCodeBase64 { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public string? CorrelationId { get; private set; }

    public static Payment CreateCard(
        Guid giftId,
        string contributorName,
        string message,
        string payerEmail,
        string payerDocType,
        string payerDocNumber,
        Guid? contributionId,
        string orderId,
        string method,
        decimal amount,
        int installments,
        string status,
        string? statusDetail,
        string? mpOrderId,
        string? mpPaymentId)
        => CreateCard(
            giftId,
            string.Empty,
            contributorName,
            message,
            payerEmail,
            payerDocType,
            payerDocNumber,
            contributionId,
            orderId,
            method,
            amount,
            installments,
            status,
            statusDetail,
            mpOrderId,
            mpPaymentId);

    public static Payment CreateCard(
        Guid giftId,
        string giftName,
        string contributorName,
        string message,
        string payerEmail,
        string payerDocType,
        string payerDocNumber,
        Guid? contributionId,
        string orderId,
        string method,
        decimal amount,
        int installments,
        string status,
        string? statusDetail,
        string? mpOrderId,
        string? mpPaymentId,
        DateTime? expiresAt = null,
        Guid? coupleId = null,
        string? correlationId = null)
    {
        Payment payment = CreateBase(
            giftId,
            giftName,
            contributorName,
            message,
            payerEmail,
            payerDocType,
            payerDocNumber,
            orderId,
            method,
            amount,
            installments,
            status,
            statusDetail,
            mpOrderId,
            mpPaymentId,
            expiresAt,
            coupleId,
            correlationId);

        if (contributionId.HasValue)
            payment.MarkContributionCreated(contributionId.Value);

        return payment;
    }

    public static Payment CreatePix(
        Guid giftId,
        string contributorName,
        string message,
        string payerEmail,
        string payerDocType,
        string payerDocNumber,
        string orderId,
        decimal amount,
        string status,
        string? statusDetail,
        string? mpOrderId,
        string? mpPaymentId,
        string pixQrCode,
        string? qrCodeBase64)
        => CreatePix(
            giftId,
            string.Empty,
            contributorName,
            message,
            payerEmail,
            payerDocType,
            payerDocNumber,
            orderId,
            amount,
            status,
            statusDetail,
            mpOrderId,
            mpPaymentId,
            pixQrCode,
            qrCodeBase64);

    public static Payment CreatePix(
        Guid giftId,
        string giftName,
        string contributorName,
        string message,
        string payerEmail,
        string payerDocType,
        string payerDocNumber,
        string orderId,
        decimal amount,
        string status,
        string? statusDetail,
        string? mpOrderId,
        string? mpPaymentId,
        string pixQrCode,
        string? qrCodeBase64,
        DateTime? expiresAt = null,
        Guid? coupleId = null,
        string? correlationId = null)
    {
        Payment payment = CreateBase(
            giftId,
            giftName,
            contributorName,
            message,
            payerEmail,
            payerDocType,
            payerDocNumber,
            orderId,
            "pix",
            amount,
            0,
            status,
            statusDetail,
            mpOrderId,
            mpPaymentId,
            expiresAt,
            coupleId,
            correlationId);

        payment.PixQrCode = pixQrCode;
        payment.QrCodeBase64 = qrCodeBase64;

        return payment;
    }

    public void UpdateProviderStatus(
        string status,
        string? statusDetail,
        string? mpOrderId = null,
        string? mpPaymentId = null,
        string? pixQrCode = null,
        string? qrCodeBase64 = null,
        decimal? refundedAmount = null)
    {
        Status = status;
        StatusDetail = statusDetail;
        MpOrderId = string.IsNullOrWhiteSpace(mpOrderId) ? MpOrderId : mpOrderId.Trim();
        MpPaymentId = mpPaymentId ?? MpPaymentId;
        PixQrCode = pixQrCode ?? PixQrCode;
        QrCodeBase64 = qrCodeBase64 ?? QrCodeBase64;
        if (refundedAmount.HasValue)
            RefundedAmount = Math.Clamp(refundedAmount.Value, 0, Amount);
        Touch();
    }

    public void MarkContributionCreated(Guid contributionId)
    {
        ContributionId = contributionId;
        ContributionCreated = true;
        if (string.IsNullOrWhiteSpace(Status))
            Status = "approved";

        Touch();
    }

    public void Expire()
    {
        Status = "expired";
        Touch();
    }

    private static Payment CreateBase(
        Guid giftId,
        string giftName,
        string contributorName,
        string message,
        string payerEmail,
        string payerDocType,
        string payerDocNumber,
        string orderId,
        string method,
        decimal amount,
        int installments,
        string status,
        string? statusDetail,
        string? mpOrderId,
        string? mpPaymentId,
        DateTime? expiresAt = null,
        Guid? coupleId = null,
        string? correlationId = null)
    {
        DateTime now = DateTime.UtcNow;

        return new Payment
        {
            Id = Guid.NewGuid(),
            CoupleId = coupleId ?? Couple.SingletonId,
            GiftId = giftId,
            GiftName = giftName.Trim(),
            ContributorName = contributorName.Trim(),
            Message = message?.Trim() ?? string.Empty,
            PayerEmail = payerEmail.Trim(),
            PayerDocType = payerDocType.Trim(),
            PayerDocNumber = payerDocNumber.Trim(),
            ContributionCreated = false,
            OrderId = orderId.Trim(),
            Method = method.Trim(),
            Amount = amount,
            Installments = installments,
            Status = status,
            StatusDetail = statusDetail,
            MpOrderId = mpOrderId,
            MpPaymentId = mpPaymentId,
            CreatedAt = now,
            UpdatedAt = now,
            ExpiresAt = expiresAt ?? now.AddMinutes(15),
            CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim()
        };
    }

    private void Touch()
        => UpdatedAt = DateTime.UtcNow;
}
