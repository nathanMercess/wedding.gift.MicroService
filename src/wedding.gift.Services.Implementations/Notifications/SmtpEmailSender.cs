using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using wedding.gift.Crosscutting.Models.Settings;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Services.Implementations.Notifications;

public class SmtpEmailSender(IOptions<EmailSettings> emailSettings, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly EmailSettings _settings = emailSettings.Value;

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_settings.Enabled || string.IsNullOrWhiteSpace(_settings.Host))
        {
            logger.LogWarning("Envio de e-mail está desabilitado. Token de confirmação não foi enviado.");
            return;
        }

        var fromAddress = string.IsNullOrWhiteSpace(_settings.FromEmail) ? _settings.UserName : _settings.FromEmail;
        var fromName = string.IsNullOrWhiteSpace(_settings.FromName) ? "Wedding Gift" : _settings.FromName;

        using var message = new MailMessage(new MailAddress(fromAddress, fromName), new MailAddress(to))
        {
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };

        using var client = new SmtpClient(_settings.Host, _settings.Port)
        {
            EnableSsl = _settings.EnableSsl,
            Credentials = new NetworkCredential(_settings.UserName, _settings.Password)
        };

        await client.SendMailAsync(message);
    }
}
