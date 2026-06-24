namespace wedding.gift.Services.Contracts;

public interface IEmailService
{
    Task SendErrorNotificationAsync(string subject, string body, CancellationToken cancellationToken = default);
    Task SendEmailConfirmationAsync(string toEmail, string toName, string token, CancellationToken cancellationToken = default);
    Task SendContributionNotificationAsync(string contributorName, decimal amount, CancellationToken cancellationToken = default);
    Task SendPaymentAttemptNotificationAsync(string subject, string body, CancellationToken cancellationToken = default);
}
