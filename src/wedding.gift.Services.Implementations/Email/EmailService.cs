using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net;
using System.Net.Mail;
using wedding.gift.Crosscutting.Models.Configurations;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Services.Implementations.Email;

public sealed class EmailService(
    IOptions<SmtpOptions> smtpOptions,
    IOptions<ApiOptions> apiOptions,
    ILogger<EmailService> logger) : IEmailService
{
    private const string EmailConfirmationTemplate = "EmailConfirmation.html";
    private const string ContributionNotificationTemplate = "ContributionNotification.html";
    private const string SystemNotificationTemplate = "SystemNotification.html";

    private readonly SmtpOptions _smtp = smtpOptions.Value;
    private readonly ApiOptions _api = apiOptions.Value;

    public Task SendEmailConfirmationAsync(string toEmail, string toName, string token, CancellationToken cancellationToken)
    {
        string confirmUrl = $"{_api.BaseUrl.TrimEnd('/')}/api/auth/confirm-email" +
                            $"?email={Uri.EscapeDataString(toEmail)}&token={Uri.EscapeDataString(token)}";

        string body = EmailTemplateRenderer.Render(EmailConfirmationTemplate, new Dictionary<string, string>
        {
            ["RecipientName"] = WebUtility.HtmlEncode(toName),
            ["ConfirmUrl"] = WebUtility.HtmlEncode(confirmUrl)
        });

        return SendAsync(toEmail, toName, "Confirme seu e-mail - Wedding Gift", body, cancellationToken);
    }

    public Task SendErrorNotificationAsync(string subject, string body, CancellationToken cancellationToken)
    {
        string recipient = string.IsNullOrWhiteSpace(_smtp.ErrorNotificationRecipient)
            ? _smtp.FromEmail
            : _smtp.ErrorNotificationRecipient;

        string html = RenderSystemNotification(subject, body);

        return SendAsync(recipient, "Suporte", subject, html, cancellationToken);
    }

    public Task SendPaymentAttemptNotificationAsync(string subject, string body, CancellationToken cancellationToken)
    {
        string recipient = string.IsNullOrWhiteSpace(_smtp.ErrorNotificationRecipient)
            ? _smtp.FromEmail
            : _smtp.ErrorNotificationRecipient;

        string html = RenderSystemNotification(subject, body);

        return SendAsync(recipient, "Suporte", subject, html, cancellationToken);
    }

    public Task SendContributionNotificationAsync(string contributorName, decimal amount, CancellationToken cancellationToken)
    {
        string recipient = string.IsNullOrWhiteSpace(_smtp.CoupleNotificationRecipient)
            ? _smtp.FromEmail
            : _smtp.CoupleNotificationRecipient;

        string amountText = amount.ToString("C", new CultureInfo("pt-BR"));

        string body = EmailTemplateRenderer.Render(ContributionNotificationTemplate, new Dictionary<string, string>
        {
            ["ContributorName"] = WebUtility.HtmlEncode(contributorName),
            ["Amount"] = WebUtility.HtmlEncode(amountText)
        });

        return SendAsync(recipient, "Casal", "Nova contribuicao recebida - Wedding Gift", body, cancellationToken);
    }

    private static string RenderSystemNotification(string subject, string body)
        => EmailTemplateRenderer.Render(SystemNotificationTemplate, new Dictionary<string, string>
        {
            ["Title"] = WebUtility.HtmlEncode(subject),
            ["Body"] = WebUtility.HtmlEncode(body)
        });

    private async Task SendAsync(string toEmail, string toName, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        if (!TryCreateMailAddress(_smtp.FromEmail, _smtp.FromName, out MailAddress fromAddress))
        {
            logger.LogWarning("E-mail nao enviado: remetente SMTP invalido.");
            return;
        }

        if (!TryCreateMailAddress(toEmail, toName, out MailAddress toAddress))
        {
            logger.LogWarning("E-mail nao enviado: destinatario invalido para o assunto {Subject}.", subject);
            return;
        }

        string safeSubject = NormalizeHeaderText(subject);

        try
        {
            using MailMessage message = new()
            {
                From = fromAddress,
                Subject = safeSubject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(toAddress);

            using SmtpClient client = new(_smtp.Host, _smtp.Port)
            {
                Credentials = new NetworkCredential(_smtp.Username, _smtp.Password),
                EnableSsl = _smtp.EnableSsl,
                Timeout = 15_000
            };

            await client.SendMailAsync(message, cancellationToken);

            logger.LogInformation("E-mail enviado para {Recipient} (assunto: {Subject}).", toAddress.Address, safeSubject);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao enviar e-mail para {Recipient} (assunto: {Subject}).", toAddress.Address, safeSubject);
            throw new EmailDeliveryException(toAddress.Address, safeSubject, ex);
        }
    }

    private static bool TryCreateMailAddress(string email, string displayName, out MailAddress address)
    {
        address = null!;
        string normalizedEmail = NormalizeHeaderText(email);
        string normalizedDisplayName = NormalizeHeaderText(displayName);

        if (string.IsNullOrWhiteSpace(normalizedEmail))
            return false;

        try
        {
            address = new MailAddress(normalizedEmail, normalizedDisplayName);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string NormalizeHeaderText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return new string(value.Trim().Where(c => !char.IsControl(c)).ToArray());
    }
}
