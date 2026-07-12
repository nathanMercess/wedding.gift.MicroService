using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.Configurations;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Implementations;
using Xunit;

namespace wedding.gift.Tests;

public sealed class MercadoPagoServiceTests
{
    [Fact]
    public async Task CreatePixOrderAsync_DeveMapearActionRequiredEPersistirExpiracaoNoRequest()
    {
        CaptureHandler handler = new()
        {
            Response = new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("""
                    {
                      "id": "ORD_PIX_1",
                      "status": "action_required",
                      "status_detail": "waiting_transfer",
                      "transactions": {
                        "payments": [{
                          "id": "PAY_PIX_1",
                          "status": "action_required",
                          "status_detail": "waiting_transfer",
                          "payment_method": {
                            "qr_code": "pix-code",
                            "qr_code_base64": "base64-code"
                          }
                        }]
                      }
                    }
                    """, Encoding.UTF8, "application/json")
            }
        };
        MercadoPagoService service = CreateService(handler);

        PaymentResponseDto result = await service.CreatePixOrderAsync(new PixPaymentRequestDto
        {
            GiftId = Guid.NewGuid(),
            ContributorName = "Ana",
            OrderId = "order-1",
            Amount = 100m,
            PayerEmail = "ana@example.com",
            PayerDocNumber = "12345678909"
        }, CancellationToken.None);

        Assert.Equal(PaymentStatuses.ActionRequired, result.Status);
        Assert.Equal("ORD_PIX_1", result.MpOrderId);
        Assert.Equal("pix-code", result.QrCode);
        Assert.Contains("\"expiration_time\":\"PT30M\"", handler.RequestBody);
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

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return Response;
        }
    }
}
