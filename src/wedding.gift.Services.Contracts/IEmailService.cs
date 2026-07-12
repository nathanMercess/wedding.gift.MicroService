namespace wedding.gift.Services.Contracts;

public interface IEmailService
{
    Task SendErrorNotificationAsync(string subject, string body, CancellationToken cancellationToken);
    Task SendEmailConfirmationAsync(string toEmail, string toName, string token, CancellationToken cancellationToken);
    Task SendContributionNotificationAsync(string contributorName, decimal amount, CancellationToken cancellationToken);
    Task SendGuestReceiptAsync(string toEmail, string contributorName, string giftName, string orderId, decimal amount, CancellationToken cancellationToken)
        => Task.CompletedTask;
    Task SendGuestReceiptAsync(
        string toEmail,
        string contributorName,
        string coupleNames,
        string giftName,
        string orderId,
        decimal amount,
        string method,
        DateTime paymentDateUtc,
        string message,
        string lookupToken,
        CancellationToken cancellationToken)
        => SendGuestReceiptAsync(toEmail, contributorName, giftName, orderId, amount, cancellationToken);
    Task SendPaymentAttemptNotificationAsync(string subject, string body, CancellationToken cancellationToken);
    Task SendPasswordResetAsync(string toEmail, string toName, string token, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
