namespace wedding.gift.Services.Contracts;

public interface IEmailService
{
    Task SendEmailConfirmationAsync(string toEmail, string toName, string token, CancellationToken cancellationToken);
    Task SendErrorNotificationAsync(string subject, string body, CancellationToken cancellationToken = default);
}
