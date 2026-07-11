namespace wedding.gift.Services.Implementations.Email;

public sealed class EmailDeliveryException(string recipient, string subject, Exception innerException)
    : Exception($"Falha ao enviar e-mail para '{recipient}' (assunto: '{subject}').", innerException)
{
    public string Recipient { get; } = recipient;
    public string Subject { get; } = subject;
}
