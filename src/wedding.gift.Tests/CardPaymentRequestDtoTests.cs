using System.ComponentModel.DataAnnotations;
using wedding.gift.Crosscutting.Models.DTOs;
using Xunit;

namespace wedding.gift.Tests;

public sealed class CardPaymentRequestDtoTests
{
    [Fact]
    public void Validation_DeveAceitarDeviceIdGeradoPeloMercadoPago()
    {
        CardPaymentRequestDto request = CreateValidRequest();
        request.DeviceId = $"armor.{new string('a', 192)}.{new string('b', 32)}";

        bool isValid = Validator.TryValidateObject(request, new ValidationContext(request), [], true);

        Assert.True(isValid);
    }

    [Fact]
    public void Validation_DeveRejeitarDeviceIdAcimaDoLimite()
    {
        CardPaymentRequestDto request = CreateValidRequest();
        request.DeviceId = new string('a', 513);

        bool isValid = Validator.TryValidateObject(request, new ValidationContext(request), [], true);

        Assert.False(isValid);
    }

    [Theory]
    [InlineData("0.01")]
    [InlineData("9.99")]
    [InlineData("10.999")]
    public void Validation_DeveRejeitarValorAbaixoDoMinimoOuComEscalaInvalida(string rawAmount)
    {
        CardPaymentRequestDto request = CreateValidRequest();
        request.Amount = decimal.Parse(rawAmount, System.Globalization.CultureInfo.InvariantCulture);

        bool isValid = Validator.TryValidateObject(request, new ValidationContext(request), [], true);

        Assert.False(isValid);
    }

    [Theory]
    [InlineData("10.00")]
    [InlineData("10.99")]
    [InlineData("99999999.99")]
    public void Validation_DeveAceitarValorNoIntervaloComDuasCasas(string rawAmount)
    {
        CardPaymentRequestDto request = CreateValidRequest();
        request.Amount = decimal.Parse(rawAmount, System.Globalization.CultureInfo.InvariantCulture);

        bool isValid = Validator.TryValidateObject(request, new ValidationContext(request), [], true);

        Assert.True(isValid);
    }

    private static CardPaymentRequestDto CreateValidRequest()
        => new()
        {
            GiftId = Guid.NewGuid(),
            ContributorName = "Contribuinte",
            CardToken = "card-token",
            OrderId = Guid.NewGuid().ToString("D"),
            Amount = 50m,
            Installments = 1,
            Method = "credit_card",
            PaymentMethodId = "visa",
            IssuerId = "25",
            PayerEmail = "contribuinte@example.com",
            PayerDocType = "CPF",
            PayerDocNumber = "12345678901"
        };
}
