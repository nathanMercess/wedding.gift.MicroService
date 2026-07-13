using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;
using System.Text.Json;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.Configurations;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Implementations;
using Xunit;

namespace wedding.gift.Tests;

public sealed class MercadoPagoServiceTests
{
    [Fact]
    public async Task CreatePixOrderAsync_DeveUsarPaymentsApiMapearQrCodeEPersistirExpiracaoNoRequest()
    {
        CaptureHandler handler = new()
        {
            Response = new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("""
                    {
                      "id": 123456789,
                      "external_reference": "order-1",
                      "transaction_amount": 100.00,
                      "currency_id": "BRL",
                      "status": "pending",
                      "status_detail": "pending_waiting_transfer",
                      "payment_method_id": "pix",
                      "payment_type_id": "bank_transfer",
                      "point_of_interaction": {
                        "transaction_data": {
                            "qr_code": "pix-code",
                            "qr_code_base64": "base64-code"
                        }
                      }
                    }
                    """, Encoding.UTF8, "application/json")
            }
        };
        MercadoPagoService service = CreateService(handler);
        DateTimeOffset minimumExpiration = DateTimeOffset.UtcNow.AddMinutes(30);

        PaymentResponseDto result = await service.CreatePixOrderAsync(new PixPaymentRequestDto
        {
            GiftId = Guid.NewGuid(),
            ContributorName = "APRO",
            OrderId = "order-1",
            Amount = 100m,
            PayerEmail = "ana@example.com",
            PayerDocNumber = "12345678909"
        }, CancellationToken.None);
        DateTimeOffset maximumExpiration = DateTimeOffset.UtcNow.AddMinutes(32);
        using JsonDocument request = JsonDocument.Parse(handler.RequestBody);
        JsonElement root = request.RootElement;
        DateTimeOffset expiration = DateTimeOffset.Parse(root.GetProperty("date_of_expiration").GetString()!);

        Assert.Equal(PaymentStatuses.Pending, result.Status);
        Assert.Null(result.MpOrderId);
        Assert.Equal("123456789", result.MpPaymentId);
        Assert.Equal("order-1", result.OrderId);
        Assert.Equal(100m, result.Amount);
        Assert.Equal("BRL", result.CurrencyId);
        Assert.Equal("pix", result.Method);
        Assert.Equal("pix-code", result.QrCode);
        Assert.Equal("base64-code", result.QrCodeBase64);
        Assert.Equal("https://api.mercadopago.com/v1/payments", handler.RequestUri);
        Assert.Equal("order-1", handler.IdempotencyKey);
        Assert.Equal("pix", root.GetProperty("payment_method_id").GetString());
        Assert.Equal("APRO", root.GetProperty("payer").GetProperty("first_name").GetString());
        Assert.False(root.TryGetProperty("token", out _));
        Assert.False(root.TryGetProperty("installments", out _));
        Assert.InRange(expiration, minimumExpiration, maximumExpiration);
    }

    [Fact]
    public async Task GetOrderStatusAsync_DeveConsultarPaymentEMapearQrCode()
    {
        CaptureHandler handler = new()
        {
            Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {
                      "id": "987654321",
                      "external_reference": "order-pix",
                      "transaction_amount": 50.00,
                      "currency_id": "BRL",
                      "status": "approved",
                      "status_detail": "accredited",
                      "payment_method_id": "pix",
                      "payment_type_id": "bank_transfer",
                      "point_of_interaction": {
                        "transaction_data": {
                          "qr_code": "pix-status-code",
                          "qr_code_base64": "pix-status-base64"
                        }
                      }
                    }
                    """, Encoding.UTF8, "application/json")
            }
        };
        MercadoPagoService service = CreateService(handler);

        PaymentResponseDto result = await service.GetOrderStatusAsync("987654321", CancellationToken.None);

        Assert.Equal("https://api.mercadopago.com/v1/payments/987654321", handler.RequestUri);
        Assert.Equal(PaymentStatuses.Approved, result.Status);
        Assert.Null(result.MpOrderId);
        Assert.Equal("987654321", result.MpPaymentId);
        Assert.Equal("order-pix", result.OrderId);
        Assert.Equal(50m, result.Amount);
        Assert.Equal("BRL", result.CurrencyId);
        Assert.Equal("pix", result.Method);
        Assert.Equal("pix-status-code", result.QrCode);
        Assert.Equal("pix-status-base64", result.QrCodeBase64);
    }

    [Fact]
    public async Task GetOrderStatusAsync_DeveMapearValorMoedaMetodoEReembolsoParcial()
    {
        CaptureHandler handler = new()
        {
            Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {
                      "id": "ORD_PIX_STATUS",
                      "external_reference": "9be40c80-1c87-4e94-b00a-f3ce9ce56b40",
                      "total_amount": "100.00",
                      "country_code": "BR",
                      "status": "processed",
                      "transactions": {
                        "payments": [{
                          "id": "PAY_PIX_STATUS",
                          "amount": "100.00",
                          "refunded_amount": "35.00",
                          "status": "processed",
                          "payment_method": { "id": "pix", "type": "bank_transfer" }
                        }]
                      }
                    }
                    """, Encoding.UTF8, "application/json")
            }
        };
        MercadoPagoService service = CreateService(handler);

        PaymentResponseDto result = await service.GetOrderStatusAsync("ORD_PIX_STATUS", CancellationToken.None);

        Assert.Equal(100m, result.Amount);
        Assert.Equal(35m, result.RefundedAmount);
        Assert.Equal("BRL", result.CurrencyId);
        Assert.Equal("pix", result.Method);
    }

    [Fact]
    public async Task GetChargebackAsync_DeveMapearContestacaoEmAnalise()
    {
        CaptureHandler handler = new()
        {
            Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {
                      "id": "CHB_1",
                      "payments": [123456789],
                      "currency": "BRL",
                      "amount": "100.00",
                      "coverage_applied": null
                    }
                    """, Encoding.UTF8, "application/json")
            }
        };
        MercadoPagoService service = CreateService(handler);

        PaymentResponseDto result = await service.GetChargebackAsync("CHB_1", CancellationToken.None);

        Assert.Equal(PaymentStatuses.ChargedBack, result.Status);
        Assert.Equal("in_process", result.StatusDetail);
        Assert.Equal("123456789", result.MpPaymentId);
        Assert.Equal(100m, result.RefundedAmount);
        Assert.Equal("BRL", result.CurrencyId);
    }

    [Fact]
    public async Task GetChargebackAsync_DeveRestaurarPagamentoQuandoCoberturaEhFavoravel()
    {
        CaptureHandler handler = new()
        {
            Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {
                      "id": "CHB_2",
                      "payments": ["123456789"],
                      "currency": "BRL",
                      "amount": 100.00,
                      "coverage_applied": "true"
                    }
                    """, Encoding.UTF8, "application/json")
            }
        };
        MercadoPagoService service = CreateService(handler);

        PaymentResponseDto result = await service.GetChargebackAsync("CHB_2", CancellationToken.None);

        Assert.Equal(PaymentStatuses.Approved, result.Status);
        Assert.Equal("reimbursed", result.StatusDetail);
        Assert.Equal(0m, result.RefundedAmount);
    }

    private static MercadoPagoService CreateService(HttpMessageHandler handler)
        => new(
            new HttpClient(handler),
            Options.Create(new MercadoPagoOptions
            {
                AccessToken = "test-access-token",
                BaseUrl = "https://api.mercadopago.com",
                WebhookSecret = new string('s', 32)
            }),
            NullLogger<MercadoPagoService>.Instance);

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.OK);
        public string RequestBody { get; private set; } = string.Empty;
        public string RequestUri { get; private set; } = string.Empty;
        public string IdempotencyKey { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri?.ToString() ?? string.Empty;
            IdempotencyKey = request.Headers.TryGetValues("X-Idempotency-Key", out IEnumerable<string>? values)
                ? values.Single()
                : string.Empty;
            RequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return Response;
        }
    }
}
