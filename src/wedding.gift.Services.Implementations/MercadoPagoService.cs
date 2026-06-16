using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Crosscutting.Models.DTOs.MercadoPago;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Services.Implementations;

public class MercadoPagoService(
    HttpClient httpClient,
    IConfiguration configuration) : IMercadoPagoService
{
    public async Task<PaymentResponseDto> CreateCardOrderAsync(
        CardPaymentRequestDto request,
        CancellationToken cancellationToken)
    {
        var order = new MercadoPagoOrderRequest
        {
            Type = "online",
            ProcessingMode = "automatic",
            TotalAmount = request.Amount.ToString("F2", CultureInfo.InvariantCulture),
            ExternalReference = request.OrderId,
            Payer = new OrderPayer
            {
                Email = request.PayerEmail,
                Identification = new OrderPayerIdentification
                {
                    Type = request.PayerDocType,
                    Number = request.PayerDocNumber
                }
            },
            Transactions = new OrderTransactions
            {
                Payments = new List<OrderPayment>
                {
                    new()
                    {
                        Amount = request.Amount.ToString("F2", CultureInfo.InvariantCulture),
                        PaymentMethod = new OrderPaymentMethod
                        {
                            Id = request.PaymentMethodId,
                            Type = request.Method,
                            Token = request.CardToken,
                            Installments = request.Installments,
                        }
                    }
                }
            }
        };

        return await SendOrderAsync(order, cancellationToken);
    }

    public async Task<PaymentResponseDto> CreatePixOrderAsync(
        PixPaymentRequestDto request,
        CancellationToken cancellationToken)
    {
        var order = new MercadoPagoOrderRequest
        {
            Type = "online",
            ProcessingMode = "automatic",
            TotalAmount = request.Amount.ToString("F2", CultureInfo.InvariantCulture),
            ExternalReference = request.OrderId,
            Payer = new OrderPayer
            {
                Email = request.PayerEmail,
                Identification = new OrderPayerIdentification
                {
                    Type = request.PayerDocType,
                    Number = request.PayerDocNumber
                }
            },
            Transactions = new OrderTransactions
            {
                Payments = new List<OrderPayment>
                {
                    new()
                    {
                        Amount = request.Amount.ToString("F2", CultureInfo.InvariantCulture),
                        PaymentMethod = new OrderPaymentMethod
                        {
                            Id = "pix",
                            Type = "bank_transfer"
                        }
                    }
                }
            }
        };

        return await SendOrderAsync(order, cancellationToken);
    }

    public async Task<PaymentResponseDto> GetOrderStatusAsync(
        string mpOrderId,
        CancellationToken cancellationToken)
    {
        try
        {
            var accessToken = configuration["MercadoPago:AccessToken"];
            var baseUrl = configuration["MercadoPago:BaseUrl"];

            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.GetAsync(
                $"{baseUrl}/v1/orders/{mpOrderId}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                return new PaymentResponseDto
                {
                    Status = "error",
                    ErrorCode = PaymentErrorCodes.ProviderError,
                    Message = $"Failed to query order status: {error}"
                };
            }

            var order = await response.Content.ReadFromJsonAsync<MercadoPagoOrderResponse>(
                cancellationToken: cancellationToken);

            return MapResponse(order);
        }
        catch (Exception ex)
        {
            return new PaymentResponseDto
            {
                Status = "error",
                ErrorCode = PaymentErrorCodes.ProviderError,
                Message = $"Error querying order status: {ex.Message}"
            };
        }
    }

    private async Task<PaymentResponseDto> SendOrderAsync(
        MercadoPagoOrderRequest order,
        CancellationToken cancellationToken)
    {
        try
        {
            var accessToken = configuration["MercadoPago:AccessToken"];
            var baseUrl = configuration["MercadoPago:BaseUrl"];

            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            httpClient.DefaultRequestHeaders.Remove("X-Idempotency-Key");
            httpClient.DefaultRequestHeaders.Add("X-Idempotency-Key", Guid.NewGuid().ToString());

            var response = await httpClient.PostAsJsonAsync(
                $"{baseUrl}/v1/orders",
                order,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                return new PaymentResponseDto
                {
                    Status = "error",
                    ErrorCode = PaymentErrorCodes.ProviderError,
                    Message = $"Failed to create order: {error}"
                };
            }

            var result = await response.Content.ReadFromJsonAsync<MercadoPagoOrderResponse>(
                cancellationToken: cancellationToken);

            return MapResponse(result);
        }
        catch (Exception ex)
        {
            return new PaymentResponseDto
            {
                Status = "error",
                ErrorCode = PaymentErrorCodes.ProviderError,
                Message = $"Error processing order: {ex.Message}"
            };
        }
    }

    private PaymentResponseDto MapResponse(MercadoPagoOrderResponse? order)
    {
        var payment = order?.Transactions?.Payments?.FirstOrDefault();

        // "processed" no nível da Order equivale a "approved"
        var orderStatus = order?.Status == "processed" ? "approved" : order?.Status;
        var paymentStatus = payment?.Status == "processed" ? "approved" : payment?.Status;
        var finalStatus = paymentStatus ?? orderStatus ?? "error";
        var statusDetail = payment?.StatusDetail ?? order?.StatusDetail;

        var errorCode = finalStatus switch
        {
            "rejected" => statusDetail == "cc_rejected_card_disabled"
                ? PaymentErrorCodes.InvalidCardToken
                : PaymentErrorCodes.PaymentDeclined,
            "error" => PaymentErrorCodes.ProviderError,
            _ => null
        };

        return new PaymentResponseDto
        {
            Status = finalStatus,
            StatusDetail = statusDetail,
            ErrorCode = errorCode,
            MpOrderId = order?.Id,
            MpPaymentId = payment?.Id,
            QrCode = payment?.PaymentMethod?.QrCode,
            QrCodeBase64 = payment?.PaymentMethod?.QrCodeBase64
        };
    }
}
