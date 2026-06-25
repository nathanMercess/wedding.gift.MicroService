namespace wedding.gift.Domain.Model.Entities;

public sealed class Payment
{
    private Payment()
    {
    }

    public Guid Id { get; private set; }
    public Guid GiftId { get; private set; }
    public string ContributorName { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string PayerEmail { get; private set; } = string.Empty;
    public string PayerDocType { get; private set; } = string.Empty;
    public string PayerDocNumber { get; private set; } = string.Empty;
    public Guid? ContributionId { get; private set; }
    public Contribution? Contribution { get; private set; }
    public bool ContributionCreated { get; private set; }
    public string OrderId { get; private set; } = string.Empty;
    public string Method { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public int Installments { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public string? StatusDetail { get; private set; }
    public string Nsu { get; private set; } = string.Empty;
    public string? MpOrderId { get; private set; }
    public string? MpPaymentId { get; private set; }
    public string PixQrCode { get; private set; } = string.Empty;
    public string? QrCodeBase64 { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

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
    {
        Payment payment = CreateBase(
            giftId,
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
            mpPaymentId);

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
    {
        Payment payment = CreateBase(
            giftId,
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
            mpPaymentId);

        payment.PixQrCode = pixQrCode;
        payment.QrCodeBase64 = qrCodeBase64;

        return payment;
    }

    public void UpdateProviderStatus(string status, string? statusDetail, string? mpPaymentId = null)
    {
        Status = status;
        StatusDetail = statusDetail;
        MpPaymentId = mpPaymentId ?? MpPaymentId;
        Touch();
    }

    public void MarkContributionCreated(Guid contributionId)
    {
        ContributionId = contributionId;
        ContributionCreated = true;
        Status = "approved";
        Touch();
    }

    private static Payment CreateBase(
        Guid giftId,
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
        string? mpPaymentId)
    {
        DateTime now = DateTime.UtcNow;

        return new Payment
        {
            Id = Guid.NewGuid(),
            GiftId = giftId,
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
            UpdatedAt = now
        };
    }

    private void Touch()
        => UpdatedAt = DateTime.UtcNow;
}
