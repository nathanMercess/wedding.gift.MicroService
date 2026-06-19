using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Crosscutting.Models.DTOs.MercadoPago;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Services.Implementations;

public class MercadoPagoService(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<MercadoPagoService> logger) : IMercadoPagoService
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
        var baseUrl = configuration["MercadoPago:BaseUrl"];

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/v1/orders/{mpOrderId}");
        ApplyAuth(httpRequest);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(httpRequest, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha de transporte ao consultar order {MpOrderId} no Mercado Pago.", mpOrderId);
            return ProviderError("Falha de comunicação ao consultar o status do pagamento.");
        }

        var requestId = ExtractRequestId(response);

        if (!response.IsSuccessStatusCode)
        {
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            var mpError = TryParseMpError(raw);

            logger.LogError("Erro MP {Status} ao consultar order {MpOrderId}. x-request-id={RequestId} payload={Payload}",
                (int)response.StatusCode, mpOrderId, requestId, raw);

            return new PaymentResponseDto
            {
                Status = "error",
                ErrorCode = MapMpError((int)response.StatusCode, mpError),
                Message = mpError?.Message ?? "Falha ao consultar o status do pagamento.",
                MpRequestId = requestId
            };
        }

        var order = await response.Content.ReadFromJsonAsync<MercadoPagoOrderResponse>(cancellationToken: cancellationToken);

        var dto = MapResponse(order);
        dto.MpRequestId = requestId;
        return dto;
    }

    private async Task<PaymentResponseDto> SendOrderAsync(
        MercadoPagoOrderRequest order,
        CancellationToken cancellationToken)
    {
        var baseUrl = configuration["MercadoPago:BaseUrl"];

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v1/orders")
        {
            Content = JsonContent.Create(order)
        };
        ApplyAuth(httpRequest);

        // Idempotência DETERMINÍSTICA por tentativa lógica (order + método):
        // o mesmo clique/retry reusa a chave -> o MP deduplica -> sem cobrança duplicada.
        var methodId = order.Transactions?.Payments?.FirstOrDefault()?.PaymentMethod?.Id ?? "order";
        httpRequest.Headers.Add("X-Idempotency-Key", $"{order.ExternalReference}:{methodId}");

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(httpRequest, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha de transporte ao criar order no Mercado Pago. ExternalReference={Ref}", order.ExternalReference);
            return ProviderError("Falha de comunicação com o provedor de pagamento.");
        }

        var requestId = ExtractRequestId(response);

        if (!response.IsSuccessStatusCode)
        {
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            var mpError = TryParseMpError(raw);

            logger.LogError("Erro MP {Status} ao criar order. x-request-id={RequestId} ExternalReference={Ref} payload={Payload}",
                (int)response.StatusCode, requestId, order.ExternalReference, raw);

            return new PaymentResponseDto
            {
                Status = "error",
                ErrorCode = MapMpError((int)response.StatusCode, mpError),
                Message = mpError?.Message ?? "Erro ao processar o pagamento.",
                MpRequestId = requestId
            };
        }

        var result = await response.Content.ReadFromJsonAsync<MercadoPagoOrderResponse>(cancellationToken: cancellationToken);

        var dto = MapResponse(result);
        dto.MpRequestId = requestId;
        return dto;
    }

    private void ApplyAuth(HttpRequestMessage request)
    {
        var accessToken = configuration["MercadoPago:AccessToken"];
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    private static string? ExtractRequestId(HttpResponseMessage response)
        => response.Headers.TryGetValues("x-request-id", out var values) ? values.FirstOrDefault() : null;

    private static PaymentResponseDto ProviderError(string message, string? requestId = null) => new()
    {
        Status = "error",
        ErrorCode = PaymentErrorCodes.ProviderError,
        Message = message,
        MpRequestId = requestId
    };

    private static MpError? TryParseMpError(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            return JsonSerializer.Deserialize<MpError>(raw, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string MapMpError(int httpStatus, MpError? error)
    {
        var code = error?.Code
            ?? error?.Cause?.FirstOrDefault()?.Code
            ?? error?.Errors?.FirstOrDefault()?.Code;

        return (httpStatus, code) switch
        {
            (401, _) => "INVALID_ACCESS_TOKEN",
            (_, "invalid_properties") => PaymentErrorCodes.ValidationError,
            (_, "empty_required_header") => PaymentErrorCodes.ValidationError,
            (_, "missing customer id") => PaymentErrorCodes.ValidationError,
            (_, "refund_amount_exceeds") => PaymentErrorCodes.ValidationError,
            (409, _) => "IDEMPOTENCY_KEY_ALREADY_USED",
            (423, _) => "RESOURCE_LOCKED",
            (500, _) => PaymentErrorCodes.ProviderError,
            (400, _) => PaymentErrorCodes.ValidationError,
            _ => PaymentErrorCodes.ProviderError
        };
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

    private sealed class MpError
    {
        [JsonPropertyName("message")] public string? Message { get; set; }
        [JsonPropertyName("error")] public string? Error { get; set; }
        [JsonPropertyName("code")] public string? Code { get; set; }
        [JsonPropertyName("cause")] public List<MpErrorCause>? Cause { get; set; }
        [JsonPropertyName("errors")] public List<MpErrorItem>? Errors { get; set; }
    }

    private sealed class MpErrorCause
    {
        [JsonPropertyName("code")] public string? Code { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
    }

    private sealed class MpErrorItem
    {
        [JsonPropertyName("code")] public string? Code { get; set; }
        [JsonPropertyName("message")] public string? Message { get; set; }
    }
}
