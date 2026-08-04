using wedding.gift.Crosscutting.Constants;
using wedding.gift.Domain.Model.Entities;
using Xunit;

namespace wedding.gift.Tests;

public sealed class PaymentStateMachineTests
{
    [Theory]
    [InlineData(PaymentStatuses.Pending)]
    [InlineData(PaymentStatuses.InProcess)]
    [InlineData(PaymentStatuses.Rejected)]
    [InlineData(PaymentStatuses.Expired)]
    public void UpdateProviderStatus_NaoDeveRegredirPagamentoAprovado(string nextStatus)
    {
        Payment payment = Create(PaymentStatuses.Approved);

        bool accepted = payment.UpdateProviderStatus(nextStatus, "late_event");

        Assert.False(accepted);
        Assert.Equal(PaymentStatuses.Approved, payment.Status);
    }

    [Fact]
    public void UpdateProviderStatus_DevePermitirReembolsoDepoisDaAprovacao()
    {
        Payment payment = Create(PaymentStatuses.Approved);

        bool accepted = payment.UpdateProviderStatus(PaymentStatuses.Refunded, "refunded", refundedAmount: 100m);

        Assert.True(accepted);
        Assert.Equal(PaymentStatuses.Refunded, payment.Status);
        Assert.Equal(100m, payment.RefundedAmount);
    }

    [Fact]
    public void UpdateProviderStatus_DevePermitirAprovacaoTardiaDepoisDaExpiracaoLocal()
    {
        Payment payment = Create(PaymentStatuses.Expired);

        bool accepted = payment.UpdateProviderStatus(PaymentStatuses.Approved, "accredited");

        Assert.True(accepted);
        Assert.Equal(PaymentStatuses.Approved, payment.Status);
    }

    private static Payment Create(string status)
        => Payment.CreateCard(
            Guid.NewGuid(),
            "Presente",
            "Convidado",
            string.Empty,
            "qa@example.com",
            "CPF",
            "12345678909",
            null,
            Guid.NewGuid().ToString("D"),
            "credit_card",
            100m,
            1,
            status,
            null,
            null,
            null);
}
