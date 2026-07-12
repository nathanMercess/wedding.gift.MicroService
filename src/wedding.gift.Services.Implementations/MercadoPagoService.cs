using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Crosscutting.Models.DTOs.MercadoPago;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Services.Implementations;

public sealed class MercadoPagoService(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<MercadoPagoService> logger) : IMercadoPagoService
{
    public async Task<PaymentResponseDto> CreateCardOrderAsync(
        CardPaymentRequestDto request,
        CancellationToken cancellationToken)
    {
        MercadoPagoPaymentRequest payment = new()
        {
            TransactionAmount = request.Amount,
            Token = request.CardToken,
            Description = "Wedding gift",
            Installments = request.Installments,
            PaymentMethodId = request.PaymentMethodId,
            IssuerId = request.IssuerId,
            ExternalReference = request.OrderId,
            Payer = new MercadoPagoPaymentPayer
            {
                Email = request.PayerEmail,
                Identification = new MercadoPagoPaymentIdentification
                {
                    Type = request.PayerDocType,
                    Number = request.PayerDocNumber
                }
            }
        };

        return await SendPaymentAsync(payment, request.DeviceId, cancellationToken);
    }

    public async Task<PaymentResponseDto> CreatePixOrderAsync(
        PixPaymentRequestDto request,
        CancellationToken cancellationToken)
    {
        MercadoPagoOrderRequest order = new()
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
        => mpOrderId.StartsWith("ORD", StringComparison.OrdinalIgnoreCase)
            ? await GetOrderStatusFromOrderAsync(mpOrderId, cancellationToken)
            : await GetPaymentStatusFromPaymentAsync(mpOrderId, cancellationToken);

    private async Task<PaymentResponseDto> GetOrderStatusFromOrderAsync(
        string mpOrderId,
        CancellationToken cancellationToken)
    {
        string baseUrl = configuration["MercadoPago:BaseUrl"];

        using HttpRequestMessage httpRequest = new(HttpMethod.Get, $"{baseUrl}/v1/orders/{mpOrderId}");

        if (!TryApplyAuth(httpRequest, out PaymentResponseDto? authError))
            return authError!;

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

        string requestId = ExtractRequestId(response);

        if (!response.IsSuccessStatusCode)
        {
            string raw = await response.Content.ReadAsStringAsync(cancellationToken);
            MpError mpError = TryParseMpError(raw);

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

        MercadoPagoOrderResponse order = await response.Content.ReadFromJsonAsync<MercadoPagoOrderResponse>(cancellationToken: cancellationToken);

        PaymentResponseDto dto = MapResponse(order);
        dto.MpRequestId = requestId;
        return dto;
    }

    private async Task<PaymentResponseDto> GetPaymentStatusFromPaymentAsync(
        string mpPaymentId,
        CancellationToken cancellationToken)
    {
        string baseUrl = configuration["MercadoPago:BaseUrl"];

        using HttpRequestMessage httpRequest = new(HttpMethod.Get, $"{baseUrl}/v1/payments/{mpPaymentId}");

        if (!TryApplyAuth(httpRequest, out PaymentResponseDto? authError))
            return authError!;

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
            logger.LogError(ex, "Falha de transporte ao consultar payment {MpPaymentId} no Mercado Pago.", mpPaymentId);
            return ProviderError("Falha de comunicação ao consultar o status do pagamento.");
        }

        string requestId = ExtractRequestId(response);

        if (!response.IsSuccessStatusCode)
        {
            string raw = await response.Content.ReadAsStringAsync(cancellationToken);
            MpError mpError = TryParseMpError(raw);

            logger.LogError("Erro MP {Status} ao consultar payment {MpPaymentId}. x-request-id={RequestId} payload={Payload}",
                (int)response.StatusCode, mpPaymentId, requestId, raw);

            return new PaymentResponseDto
            {
                Status = "error",
                ErrorCode = MapMpError((int)response.StatusCode, mpError),
                Message = mpError?.Message ?? "Falha ao consultar o status do pagamento.",
                MpRequestId = requestId
            };
        }

        MercadoPagoPaymentResponse payment = await response.Content.ReadFromJsonAsync<MercadoPagoPaymentResponse>(cancellationToken: cancellationToken);

        PaymentResponseDto dto = MapPaymentResponse(payment);
        dto.MpRequestId = requestId;
        return dto;
    }

    private async Task<PaymentResponseDto> SendOrderAsync(
        MercadoPagoOrderRequest order,
        CancellationToken cancellationToken)
    {
        string baseUrl = configuration["MercadoPago:BaseUrl"];

        using HttpRequestMessage httpRequest = new(HttpMethod.Post, $"{baseUrl}/v1/orders")
        {
            Content = JsonContent.Create(order)
        };

        if (!TryApplyAuth(httpRequest, out PaymentResponseDto? authError))
            return authError!;

        string methodId = order.Transactions?.Payments?.FirstOrDefault()?.PaymentMethod?.Id ?? "order";
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

        string requestId = ExtractRequestId(response);

        if (!response.IsSuccessStatusCode)
        {
            string raw = await response.Content.ReadAsStringAsync(cancellationToken);
            MpError mpError = TryParseMpError(raw);

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

        MercadoPagoOrderResponse result = await response.Content.ReadFromJsonAsync<MercadoPagoOrderResponse>(cancellationToken: cancellationToken);

        PaymentResponseDto dto = MapResponse(result);
        dto.MpRequestId = requestId;
        return dto;
    }

    private async Task<PaymentResponseDto> SendPaymentAsync(
        MercadoPagoPaymentRequest payment,
        string? deviceId,
        CancellationToken cancellationToken)
    {
        string baseUrl = configuration["MercadoPago:BaseUrl"];

        using HttpRequestMessage httpRequest = new(HttpMethod.Post, $"{baseUrl}/v1/payments")
        {
            Content = JsonContent.Create(payment)
        };

        if (!TryApplyAuth(httpRequest, out PaymentResponseDto? authError))
            return authError!;

        httpRequest.Headers.Add("X-Idempotency-Key", payment.ExternalReference);

        if (!string.IsNullOrWhiteSpace(deviceId))
            httpRequest.Headers.Add("X-meli-session-id", deviceId.Trim());

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
            logger.LogError(ex, "Falha de transporte ao criar payment no Mercado Pago. ExternalReference={Ref}", payment.ExternalReference);
            return ProviderError("Falha de comunicação com o provedor de pagamento.");
        }

        string requestId = ExtractRequestId(response);
        string raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            MpError mpError = TryParseMpError(raw);

            logger.LogError("Erro MP {Status} ao criar payment. x-request-id={RequestId} ExternalReference={Ref} payload={Payload}",
                (int)response.StatusCode, requestId, payment.ExternalReference, raw);

            return new PaymentResponseDto
            {
                Status = "error",
                ErrorCode = MapMpError((int)response.StatusCode, mpError),
                Message = mpError?.Message ?? "Erro ao processar o pagamento.",
                MpRequestId = requestId
            };
        }

        MercadoPagoPaymentResponse result = JsonSerializer.Deserialize<MercadoPagoPaymentResponse>(
            raw,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        PaymentResponseDto dto = MapPaymentResponse(result);
        dto.MpRequestId = requestId;
        return dto;
    }

    private bool TryApplyAuth(HttpRequestMessage request, out PaymentResponseDto? error)
    {
        string accessToken = NormalizeAccessToken(configuration["MercadoPago:AccessToken"]);

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            logger.LogError("MercadoPago:AccessToken nao configurado.");
            error = ProviderError("Mercado Pago nao esta configurado para processar pagamentos.");
            return false;
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        error = null;
        return true;
    }

    private static string NormalizeAccessToken(string? accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return string.Empty;

        return new string(accessToken.Where(c => !char.IsControl(c)).ToArray()).Trim();
    }

    private static string? ExtractRequestId(HttpResponseMessage response)
        => response.Headers.TryGetValues("x-request-id", out IEnumerable<string> values) ? values.FirstOrDefault() : null;

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
        string code = error?.Code
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
        OrderResponsePayment payment = order?.Transactions?.Payments?.FirstOrDefault();

        string orderStatus = order?.Status;
        string paymentStatus = payment?.Status;
        string finalStatus = paymentStatus ?? orderStatus ?? "error";
        string statusDetail = payment?.StatusDetail ?? order?.StatusDetail;

        string errorCode = finalStatus switch
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

    private static PaymentResponseDto MapPaymentResponse(MercadoPagoPaymentResponse? payment)
    {
        string finalStatus = payment?.Status ?? "error";
        string? statusDetail = payment?.StatusDetail;
        string? id = payment?.IdAsString();

        string errorCode = finalStatus switch
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
            MpOrderId = id,
            MpPaymentId = id
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

    private sealed class MercadoPagoPaymentRequest
    {
        [JsonPropertyName("transaction_amount")] public decimal TransactionAmount { get; set; }
        [JsonPropertyName("token")] public string Token { get; set; } = string.Empty;
        [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
        [JsonPropertyName("installments")] public int Installments { get; set; }
        [JsonPropertyName("payment_method_id")] public string PaymentMethodId { get; set; } = string.Empty;
        [JsonPropertyName("issuer_id")] public string? IssuerId { get; set; }
        [JsonPropertyName("external_reference")] public string ExternalReference { get; set; } = string.Empty;
        [JsonPropertyName("payer")] public MercadoPagoPaymentPayer Payer { get; set; } = new();
    }

    private sealed class MercadoPagoPaymentPayer
    {
        [JsonPropertyName("email")] public string Email { get; set; } = string.Empty;
        [JsonPropertyName("identification")] public MercadoPagoPaymentIdentification Identification { get; set; } = new();
    }

    private sealed class MercadoPagoPaymentIdentification
    {
        [JsonPropertyName("type")] public string Type { get; set; } = "CPF";
        [JsonPropertyName("number")] public string Number { get; set; } = string.Empty;
    }

    private sealed class MercadoPagoPaymentResponse
    {
        [JsonPropertyName("id")] public JsonElement Id { get; set; }
        [JsonPropertyName("status")] public string? Status { get; set; }
        [JsonPropertyName("status_detail")] public string? StatusDetail { get; set; }

        public string? IdAsString()
        {
            return Id.ValueKind switch
            {
                JsonValueKind.String => Id.GetString(),
                JsonValueKind.Number => Id.GetRawText(),
                _ => null
            };
        }
    }
}
