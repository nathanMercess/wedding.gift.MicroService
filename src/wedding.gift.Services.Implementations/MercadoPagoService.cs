using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.Configurations;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Crosscutting.Models.DTOs.MercadoPago;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Services.Implementations;

public sealed class MercadoPagoService(
    HttpClient httpClient,
    IOptions<MercadoPagoOptions> mercadoPagoOptions,
    ILogger<MercadoPagoService> logger) : IMercadoPagoService
{
    private readonly MercadoPagoOptions _options = mercadoPagoOptions.Value;
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

        PaymentResponseDto result = await SendPaymentAsync(payment, request.DeviceId, cancellationToken);
        result.Amount ??= request.Amount;
        result.CurrencyId ??= "BRL";
        result.Method = string.IsNullOrWhiteSpace(result.Method) ? request.Method : result.Method;
        return result;
    }

    public async Task<PaymentResponseDto> CreatePixOrderAsync(
        PixPaymentRequestDto request,
        CancellationToken cancellationToken)
    {
        MercadoPagoPaymentRequest payment = new()
        {
            TransactionAmount = request.Amount,
            Description = "Wedding gift",
            PaymentMethodId = "pix",
            ExternalReference = request.OrderId,
            DateOfExpiration = DateTimeOffset.UtcNow.AddMinutes(31).ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture),
            Payer = new MercadoPagoPaymentPayer
            {
                FirstName = request.ContributorName,
                Email = request.PayerEmail,
                Identification = new MercadoPagoPaymentIdentification
                {
                    Type = request.PayerDocType,
                    Number = request.PayerDocNumber
                }
            }
        };

        PaymentResponseDto result = await SendPaymentAsync(payment, null, cancellationToken);
        result.Amount ??= request.Amount;
        result.CurrencyId ??= "BRL";
        result.Method = string.IsNullOrWhiteSpace(result.Method) ? "pix" : result.Method;
        return result;
    }

    public async Task<PaymentResponseDto> GetOrderStatusAsync(
        string mpOrderId,
        CancellationToken cancellationToken)
        => mpOrderId.StartsWith("ORD", StringComparison.OrdinalIgnoreCase)
            ? await GetOrderStatusFromOrderAsync(mpOrderId, cancellationToken)
            : await GetPaymentStatusFromPaymentAsync(mpOrderId, cancellationToken);

    public async Task<PaymentResponseDto> GetChargebackAsync(
        string chargebackId,
        CancellationToken cancellationToken)
    {
        string baseUrl = _options.BaseUrl.TrimEnd('/');
        using HttpRequestMessage request = new(HttpMethod.Get, $"{baseUrl}/v1/chargebacks/{chargebackId}");

        if (!TryApplyAuth(request, out PaymentResponseDto? authError))
            return authError!;

        try
        {
            using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
            string requestId = ExtractRequestId(response);

            if (!response.IsSuccessStatusCode)
            {
                string raw = await response.Content.ReadAsStringAsync(cancellationToken);
                MpError error = TryParseMpError(raw);
                return new PaymentResponseDto
                {
                    Status = PaymentStatuses.Error,
                    ErrorCode = MapMpError((int)response.StatusCode, error),
                    Message = error?.Message ?? "Não foi possível consultar o chargeback.",
                    MpRequestId = requestId
                };
            }

            MercadoPagoChargebackResponse chargeback = await response.Content.ReadFromJsonAsync<MercadoPagoChargebackResponse>(cancellationToken: cancellationToken);
            bool? coverageApplied = chargeback.CoverageAppliedValue();
            return new PaymentResponseDto
            {
                Status = coverageApplied == true ? PaymentStatuses.Approved : PaymentStatuses.ChargedBack,
                StatusDetail = coverageApplied switch
                {
                    true => "reimbursed",
                    false => "settled",
                    null => "in_process"
                },
                MpPaymentId = chargeback.PaymentId(),
                Amount = chargeback.Amount,
                RefundedAmount = coverageApplied == true ? 0 : chargeback.Amount,
                CurrencyId = chargeback.Currency,
                MpRequestId = requestId
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha de transporte ao consultar chargeback {ChargebackId} no Mercado Pago.", chargebackId);
            return ProviderError("Falha de comunicação ao consultar o chargeback.");
        }
    }

    public async Task<PaymentResponseDto> RefundAsync(
        string? mpOrderId,
        string? mpPaymentId,
        string idempotencyKey,
        CancellationToken cancellationToken)
        => await RefundAsync(mpOrderId, mpPaymentId, null, idempotencyKey, cancellationToken);

    public async Task<PaymentResponseDto> RefundAsync(
        string? mpOrderId,
        string? mpPaymentId,
        decimal? amount,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        bool useOrderApi = !string.IsNullOrWhiteSpace(mpOrderId) &&
                           mpOrderId.StartsWith("ORD", StringComparison.OrdinalIgnoreCase);
        string? providerId = useOrderApi ? mpOrderId : mpPaymentId ?? mpOrderId;

        if (string.IsNullOrWhiteSpace(providerId))
            return ProviderError("O pagamento não possui identificador no provedor.");

        string path = useOrderApi
            ? $"/v1/orders/{providerId}/refund"
            : $"/v1/payments/{providerId}/refunds";
        HttpContent content = amount.HasValue
            ? useOrderApi
                ? JsonContent.Create(new
                {
                    transactions = new[]
                    {
                        new
                        {
                            id = mpPaymentId,
                            amount = amount.Value.ToString("F2", CultureInfo.InvariantCulture)
                        }
                    }
                })
                : JsonContent.Create(new { amount = amount.Value })
            : new StringContent(string.Empty, Encoding.UTF8, "application/json");
        using HttpRequestMessage request = new(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}{path}") { Content = content };

        if (amount.HasValue && useOrderApi && string.IsNullOrWhiteSpace(mpPaymentId))
            return ProviderError("O pagamento não possui identificador de transação para reembolso parcial.");

        if (!TryApplyAuth(request, out PaymentResponseDto? authError))
            return authError!;

        request.Headers.Add("X-Idempotency-Key", idempotencyKey);

        try
        {
            using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
            string requestId = ExtractRequestId(response);

            if (!response.IsSuccessStatusCode)
            {
                string raw = await response.Content.ReadAsStringAsync(cancellationToken);
                MpError error = TryParseMpError(raw);
                return new PaymentResponseDto
                {
                    Status = PaymentStatuses.Error,
                    ErrorCode = MapMpError((int)response.StatusCode, error),
                    Message = error?.Message ?? "Não foi possível reembolsar o pagamento.",
                    MpRequestId = requestId
                };
            }

            return new PaymentResponseDto
            {
                Status = PaymentStatuses.Refunded,
                MpOrderId = mpOrderId,
                MpPaymentId = mpPaymentId,
                MpRequestId = requestId
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha de transporte ao reembolsar pagamento. ProviderId={ProviderId}.", providerId);
            return ProviderError("Falha de comunicação ao reembolsar o pagamento.");
        }
    }

    private async Task<PaymentResponseDto> GetOrderStatusFromOrderAsync(
        string mpOrderId,
        CancellationToken cancellationToken)
    {
        string baseUrl = _options.BaseUrl.TrimEnd('/');

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

            logger.LogError("Erro MP {Status} ao consultar order {MpOrderId}. x-request-id={RequestId} code={ProviderCode}",
                (int)response.StatusCode, mpOrderId, requestId, mpError?.Code ?? mpError?.Error);

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
        string baseUrl = _options.BaseUrl.TrimEnd('/');

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

            logger.LogError("Erro MP {Status} ao consultar payment {MpPaymentId}. x-request-id={RequestId} code={ProviderCode}",
                (int)response.StatusCode, mpPaymentId, requestId, mpError?.Code ?? mpError?.Error);

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
        string baseUrl = _options.BaseUrl.TrimEnd('/');

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

            logger.LogError("Erro MP {Status} ao criar order. x-request-id={RequestId} ExternalReference={Ref} code={ProviderCode}",
                (int)response.StatusCode, requestId, order.ExternalReference, mpError?.Code ?? mpError?.Error);

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
        dto.OrderId = string.IsNullOrWhiteSpace(dto.OrderId) ? order.ExternalReference : dto.OrderId;
        dto.MpRequestId = requestId;
        return dto;
    }

    private async Task<PaymentResponseDto> SendPaymentAsync(
        MercadoPagoPaymentRequest payment,
        string? deviceId,
        CancellationToken cancellationToken)
    {
        string baseUrl = _options.BaseUrl.TrimEnd('/');

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

            logger.LogError("Erro MP {Status} ao criar payment. x-request-id={RequestId} ExternalReference={Ref} code={ProviderCode}",
                (int)response.StatusCode, requestId, payment.ExternalReference, mpError?.Code ?? mpError?.Error);

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
        dto.OrderId = string.IsNullOrWhiteSpace(dto.OrderId) ? payment.ExternalReference : dto.OrderId;
        dto.MpRequestId = requestId;
        return dto;
    }

    private bool TryApplyAuth(HttpRequestMessage request, out PaymentResponseDto? error)
    {
        string accessToken = NormalizeAccessToken(_options.AccessToken);

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
            (409, _) => PaymentErrorCodes.IdempotencyKeyAlreadyUsed,
            (423, _) => PaymentErrorCodes.ResourceLocked,
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
        decimal? refundedAmount = order?.Transactions?.Refunds is { Count: > 0 } refunds
            ? refunds.Select(x => decimal.TryParse(x.Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal amount) ? amount : 0).Sum()
            : ParseAmount(payment?.RefundedAmount);
        decimal? amount = ParseAmount(payment?.Amount) ?? ParseAmount(order?.TotalAmount);
        string? method = payment?.PaymentMethod?.Id == "pix" ? "pix" : payment?.PaymentMethod?.Type;

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
            OrderId = order?.ExternalReference,
            Status = finalStatus,
            StatusDetail = statusDetail,
            ErrorCode = errorCode,
            MpOrderId = order?.Id,
            MpPaymentId = payment?.Id,
            Amount = amount,
            CurrencyId = NormalizeCurrency(order?.CountryCode),
            Method = method ?? string.Empty,
            RefundedAmount = refundedAmount,
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
            OrderId = payment?.ExternalReference,
            Status = finalStatus,
            StatusDetail = statusDetail,
            ErrorCode = errorCode,
            MpOrderId = null,
            MpPaymentId = id,
            Amount = payment?.TransactionAmount,
            CurrencyId = payment?.CurrencyId,
            Method = string.Equals(payment?.PaymentMethodId, "pix", StringComparison.OrdinalIgnoreCase)
                ? "pix"
                : payment?.PaymentTypeId ?? payment?.PaymentMethodId ?? string.Empty,
            RefundedAmount = payment?.TransactionAmountRefunded,
            QrCode = payment?.PointOfInteraction?.TransactionData?.QrCode ?? string.Empty,
            QrCodeBase64 = payment?.PointOfInteraction?.TransactionData?.QrCodeBase64
        };
    }

    private static decimal? ParseAmount(string? value)
        => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal amount) ? amount : null;

    private static string? NormalizeCurrency(string? countryCode)
        => countryCode?.Trim().ToUpperInvariant() is "BR" or "BRA" or "BRL" ? "BRL" : countryCode;

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
        [JsonPropertyName("token")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Token { get; set; }
        [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
        [JsonPropertyName("installments")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Installments { get; set; }
        [JsonPropertyName("payment_method_id")] public string PaymentMethodId { get; set; } = string.Empty;
        [JsonPropertyName("issuer_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? IssuerId { get; set; }
        [JsonPropertyName("external_reference")] public string ExternalReference { get; set; } = string.Empty;
        [JsonPropertyName("date_of_expiration")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DateOfExpiration { get; set; }
        [JsonPropertyName("payer")] public MercadoPagoPaymentPayer Payer { get; set; } = new();
    }

    private sealed class MercadoPagoPaymentPayer
    {
        [JsonPropertyName("first_name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? FirstName { get; set; }
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
        [JsonPropertyName("external_reference")] public string? ExternalReference { get; set; }
        [JsonPropertyName("transaction_amount")] public decimal? TransactionAmount { get; set; }
        [JsonPropertyName("transaction_amount_refunded")] public decimal? TransactionAmountRefunded { get; set; }
        [JsonPropertyName("currency_id")] public string? CurrencyId { get; set; }
        [JsonPropertyName("payment_type_id")] public string? PaymentTypeId { get; set; }
        [JsonPropertyName("payment_method_id")] public string? PaymentMethodId { get; set; }
        [JsonPropertyName("point_of_interaction")] public MercadoPagoPointOfInteraction? PointOfInteraction { get; set; }

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

    private sealed class MercadoPagoPointOfInteraction
    {
        [JsonPropertyName("transaction_data")] public MercadoPagoTransactionData? TransactionData { get; set; }
    }

    private sealed class MercadoPagoTransactionData
    {
        [JsonPropertyName("qr_code")] public string? QrCode { get; set; }
        [JsonPropertyName("qr_code_base64")] public string? QrCodeBase64 { get; set; }
    }

    private sealed class MercadoPagoChargebackResponse
    {
        [JsonPropertyName("payments")] public JsonElement Payments { get; set; }
        [JsonPropertyName("currency")] public string? Currency { get; set; }
        [JsonPropertyName("amount")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public decimal Amount { get; set; }
        [JsonPropertyName("coverage_applied")] public JsonElement CoverageApplied { get; set; }

        public string? PaymentId()
        {
            JsonElement payment = Payments.ValueKind == JsonValueKind.Array
                ? Payments.EnumerateArray().FirstOrDefault()
                : Payments;
            return payment.ValueKind switch
            {
                JsonValueKind.String => payment.GetString(),
                JsonValueKind.Number => payment.GetRawText(),
                JsonValueKind.Object when payment.TryGetProperty("id", out JsonElement id) && id.ValueKind == JsonValueKind.String => id.GetString(),
                JsonValueKind.Object when payment.TryGetProperty("id", out JsonElement id) && id.ValueKind == JsonValueKind.Number => id.GetRawText(),
                _ => null
            };
        }

        public bool? CoverageAppliedValue()
        {
            if (CoverageApplied.ValueKind is JsonValueKind.True or JsonValueKind.False)
                return CoverageApplied.GetBoolean();

            if (CoverageApplied.ValueKind == JsonValueKind.String && bool.TryParse(CoverageApplied.GetString(), out bool value))
                return value;

            return null;
        }
    }
}
