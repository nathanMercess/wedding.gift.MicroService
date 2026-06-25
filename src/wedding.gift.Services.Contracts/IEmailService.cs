namespace wedding.gift.Services.Contracts;

public interface IEmailService
{
    Task SendErrorNotificationAsync(string subject, string body, CancellationToken cancellationToken);
    Task SendEmailConfirmationAsync(string toEmail, string toName, string token, CancellationToken cancellationToken);
    Task SendContributionNotificationAsync(string contributorName, decimal amount, CancellationToken cancellationToken);
    Task SendPaymentAttemptNotificationAsync(string subject, string body, CancellationToken cancellationToken);
}
