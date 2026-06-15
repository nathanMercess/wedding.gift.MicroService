using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Services.Implementations;

public class InfinitePayService(
    HttpClient httpClient,
    IConfiguration configuration) : IInfinitePayService
{
    public async Task<PaymentResponseDto> AuthorizeCardAsync(
        string cardToken,
        decimal amount,
        int installments,
        string method,
        string orderId,
        CancellationToken cancellationToken)
    {
        try
        {
            var baseUrl = configuration["InfinitePay:BaseUrl"];
            var apiKey = configuration["InfinitePay:ApiKey"];

            var payload = new
            {
                amount = (int)(amount * 100),
                capture_method = method,
                installments,
                payment = new
                {
                    type = "card",
                    token = cardToken
                },
                metadata = new
                {
                    order_id = orderId
                }
            };

            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

            var response = await httpClient.PostAsJsonAsync(
                $"{baseUrl}/v2/transactions",
                payload,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                return new PaymentResponseDto { Status = "error", Message = "Failed to authorize card payment." };

            var result = await response.Content.ReadFromJsonAsync<dynamic>(cancellationToken: cancellationToken);

            return new PaymentResponseDto
            {
                Status = result?.data?.attributes?.status?.ToString() ?? "error",
                Nsu = result?.data?.attributes?.nsu?.ToString() ?? string.Empty
            };
        }
        catch
        {
            return new PaymentResponseDto { Status = "error", Message = "Error processing card payment." };
        }
    }

    public async Task<PaymentResponseDto> CreatePixTransactionAsync(
        decimal amount,
        string orderId,
        CancellationToken cancellationToken)
    {
        try
        {
            var baseUrl = configuration["InfinitePay:BaseUrl"];
            var apiKey = configuration["InfinitePay:ApiKey"];

            var payload = new
            {
                amount = (int)(amount * 100),
                capture_method = "pix",
                metadata = new
                {
                    order_id = orderId
                }
            };

            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

            var response = await httpClient.PostAsJsonAsync(
                $"{baseUrl}/v2/transactions",
                payload,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                return new PaymentResponseDto { Status = "error", Message = "Failed to create PIX transaction." };

            var result = await response.Content.ReadFromJsonAsync<dynamic>(cancellationToken: cancellationToken);

            return new PaymentResponseDto
            {
                Status = result?.data?.attributes?.status?.ToString() ?? "pending",
                Nsu = result?.data?.attributes?.nsu?.ToString() ?? string.Empty,
                PixQrCode = result?.data?.attributes?.br_code?.ToString() ?? string.Empty
            };
        }
        catch
        {
            return new PaymentResponseDto { Status = "error", Message = "Error processing PIX transaction." };
        }
    }

    public async Task<PaymentResponseDto> GetTransactionStatusAsync(
        string nsu,
        CancellationToken cancellationToken)
    {
        try
        {
            var baseUrl = configuration["InfinitePay:BaseUrl"];
            var apiKey = configuration["InfinitePay:ApiKey"];

            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

            var response = await httpClient.GetAsync(
                $"{baseUrl}/v2/transactions/{nsu}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                return new PaymentResponseDto { Status = "error", Message = "Failed to query transaction status." };

            var result = await response.Content.ReadFromJsonAsync<dynamic>(cancellationToken: cancellationToken);

            return new PaymentResponseDto
            {
                Status = result?.data?.attributes?.status?.ToString() ?? "error",
                Nsu = nsu,
                PixQrCode = result?.data?.attributes?.br_code?.ToString() ?? string.Empty
            };
        }
        catch
        {
            return new PaymentResponseDto { Status = "error", Message = "Error querying transaction status." };
        }
    }
}
