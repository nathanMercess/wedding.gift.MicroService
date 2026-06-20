using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using wedding.gift.Crosscutting.Models.Configurations;
using wedding.gift.Services.Implementations.Email;

namespace wedding.gift.Tests;

/// <summary>
/// Integração REAL com o servidor SMTP — envia e-mail de verdade (sem fake).
/// Só executa quando a env var SMTP_HOST está definida; caso contrário é pulado,
/// para não tentar enviar e-mail no `dotnet test`/CI comum.
///
/// Para rodar de verdade:
///   SMTP_HOST=... SMTP_PORT=587 SMTP_USER=... SMTP_PASS=... SMTP_FROM=... \
///     dotnet test --filter "FullyQualifiedName~EmailServiceIntegrationTests"
/// </summary>
public class EmailServiceIntegrationTests
{
    [Fact]
    public async Task SendContributionNotification_DeveEnviarDeVerdade_QuandoSmtpConfigurado()
    {
        var host = Environment.GetEnvironmentVariable("SMTP_HOST");
        if (string.IsNullOrWhiteSpace(host))
            return; // pulado (sem SMTP_HOST)

        var service = CreateRealEmailService(host);

        // Se o SMTP recusar (auth/TLS/etc.), SendAsync lança EmailDeliveryException e o teste falha
        // mostrando o erro real do provedor. Sucesso = o servidor SMTP aceitou e enviou.
        await service.SendContributionNotificationAsync("Teste de Integração", 123.45m, CancellationToken.None);
    }

    [Fact]
    public async Task SendErrorNotification_DeveEnviarDeVerdade_QuandoSmtpConfigurado()
    {
        var host = Environment.GetEnvironmentVariable("SMTP_HOST");
        if (string.IsNullOrWhiteSpace(host))
            return; // pulado (sem SMTP_HOST)

        var service = CreateRealEmailService(host);

        await service.SendErrorNotificationAsync(
            "[TESTE] Notificação de erro do sistema",
            "Corpo de teste enviado pela suíte de integração — pode ignorar.",
            CancellationToken.None);
    }

    private static EmailService CreateRealEmailService(string host)
    {
        // TEST_EMAIL_TO permite direcionar o e-mail (ex.: seu gmail) p/ provar a ENTREGA real.
        var to = Environment.GetEnvironmentVariable("TEST_EMAIL_TO");

        var smtp = Options.Create(new SmtpOptions
        {
            Host = host,
            Port = int.TryParse(Environment.GetEnvironmentVariable("SMTP_PORT"), out var port) ? port : 587,
            Username = Environment.GetEnvironmentVariable("SMTP_USER") ?? string.Empty,
            Password = Environment.GetEnvironmentVariable("SMTP_PASS") ?? string.Empty,
            FromEmail = Environment.GetEnvironmentVariable("SMTP_FROM") ?? string.Empty,
            FromName = "Wedding Gift (TESTE)",
            EnableSsl = true,
            CoupleNotificationRecipient = to,
            ErrorNotificationRecipient = to
        });

        var api = Options.Create(new ApiOptions { BaseUrl = "https://davidmaira.com" });

        return new EmailService(smtp, api, NullLogger<EmailService>.Instance);
    }
}
