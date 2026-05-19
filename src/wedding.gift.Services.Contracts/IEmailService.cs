namespace wedding.gift.Services.Contracts;

public interface IEmailService
{
    Task SendEmailConfirmationAsync(string toEmail, string toName, string token, CancellationToken cancellationToken);
}
