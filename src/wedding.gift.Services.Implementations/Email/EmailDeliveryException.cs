namespace wedding.gift.Services.Implementations.Email;

/// <summary>
/// Lançada quando o provedor SMTP falha. Carrega contexto (destinatário/assunto)
/// para o chamador decidir o que fazer — em vez de o erro ser engolido.
/// </summary>
public sealed class EmailDeliveryException(string recipient, string subject, Exception innerException)
    : Exception($"Falha ao enviar e-mail para '{recipient}' (assunto: '{subject}').", innerException)
{
    public string Recipient { get; } = recipient;
    public string Subject { get; } = subject;
}
