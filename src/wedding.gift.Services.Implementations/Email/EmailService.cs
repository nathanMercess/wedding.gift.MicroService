using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using wedding.gift.Crosscutting.Models.Configurations;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Services.Implementations.Email;

public class EmailService(
    IOptions<SmtpOptions> smtpOptions,
    IOptions<ApiOptions> apiOptions,
    ILogger<EmailService> logger) : IEmailService
{
    private readonly SmtpOptions _smtp = smtpOptions.Value;
    private readonly ApiOptions _api = apiOptions.Value;

    public Task SendEmailConfirmationAsync(string toEmail, string toName, string token, CancellationToken cancellationToken = default)
    {
        var confirmUrl = $"{_api.BaseUrl.TrimEnd('/')}/api/auth/confirm-email" +
                         $"?email={Uri.EscapeDataString(toEmail)}&token={Uri.EscapeDataString(token)}";

        var body = $"""
            <html>
            <body style="font-family: Arial, sans-serif; color: #333;">
                <h2>Confirme seu e-mail</h2>
                <p>Ol&#225;, <strong>{WebUtility.HtmlEncode(toName)}</strong>!</p>
                <p>Clique no bot&#227;o abaixo para confirmar seu endere&#231;o de e-mail:</p>
                <p>
                    <a href="{confirmUrl}"
                       style="display:inline-block;padding:12px 24px;background:#7c3aed;color:#fff;
                              text-decoration:none;border-radius:6px;font-weight:bold;">
                        Confirmar E-mail
                    </a>
                </p>
                <p>Caso o bot&#227;o n&#227;o funcione, copie e cole o link abaixo no navegador:</p>
                <p><a href="{confirmUrl}">{confirmUrl}</a></p>
                <p style="color:#888;font-size:12px;">Este link expira em 24 horas.</p>
            </body>
            </html>
            """;

        return SendAsync(toEmail, toName, "Confirme seu e-mail — Wedding Gift", body, cancellationToken);
    }

    public Task SendErrorNotificationAsync(string subject, string body, CancellationToken cancellationToken = default)
    {
        var recipient = string.IsNullOrWhiteSpace(_smtp.ErrorNotificationRecipient)
            ? _smtp.FromEmail
            : _smtp.ErrorNotificationRecipient;

        var html = $"<pre style=\"font-family:monospace;font-size:13px;white-space:pre-wrap\">{WebUtility.HtmlEncode(body)}</pre>";

        return SendAsync(recipient, "Suporte", subject, html, cancellationToken);
    }

    private async Task SendAsync(string toEmail, string toName, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(_smtp.FromEmail, _smtp.FromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(new MailAddress(toEmail, toName));

            using var client = new SmtpClient(_smtp.Host, _smtp.Port)
            {
                Credentials = new NetworkCredential(_smtp.Username, _smtp.Password),
                EnableSsl = _smtp.EnableSsl,
                Timeout = 15_000
            };

            await client.SendMailAsync(message, cancellationToken);

            logger.LogInformation("E-mail enviado para {Recipient} (assunto: {Subject}).", toEmail, subject);
        }
        catch (Exception ex)
        {
            // NUNCA engolir: loga com contexto e propaga p/ o chamador decidir (reenvio, fila, etc.).
            logger.LogError(ex, "Falha ao enviar e-mail para {Recipient} (assunto: {Subject}).", toEmail, subject);
            throw new EmailDeliveryException(toEmail, subject, ex);
        }
    }
}
