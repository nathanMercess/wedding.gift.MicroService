using System.Net.Http.Headers;
using System.Net.Http.Json;
using wedding.gift.Application.Webapi.Models.InfinitePay;
using wedding.gift.Application.Webapi.Models.Response;
using wedding.gift.Application.Webapi.Services.Interfaces;

namespace wedding.gift.Application.Webapi.Services;

public class InfinitePayService(
    HttpClient http,
    IInfinitePayAuthService auth,
    IConfiguration config) : IInfinitePayService
{
    public async Task<PagamentoResponse> AutorizarCartaoAsync(
        string cardToken,
        decimal valor,
        int parcelas,
        string metodo,
        string pedidoId,
        CancellationToken ct = default)
    {
        try
        {
            var accessToken = await auth.GetAccessTokenAsync("transactions", ct);
            var baseUrl = config["InfinitePay:BaseUrl"];
            var webhookBase = config["InfinitePay:WebhookBaseUrl"];
            var webhookSecret = config["InfinitePay:WebhookSecret"];

            var payload = new
            {
                amount = (int)(valor * 100),
                capture_method = metodo,
                installments = parcelas,
                payment = new
                {
                    type = "card",
                    token = cardToken
                },
                metadata = new
                {
                    order_id = pedidoId,
                    callback = new
                    {
                        confirm = $"{webhookBase}/api/webhook/pagamento?pedido={pedidoId}",
                        secret = webhookSecret
                    }
                }
            };

            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await http.PostAsJsonAsync(
                $"{baseUrl}/v2/transactions",
                payload,
                ct);

            if (!response.IsSuccessStatusCode)
            {
                return new PagamentoResponse { Status = "error", Mensagem = "Falha ao autorizar pagamento com cartão." };
            }

            var resultado = await response.Content.ReadFromJsonAsync<TransacaoApiResponse>(cancellationToken: ct);
            var attrs = resultado?.Data?.Attributes;

            return new PagamentoResponse
            {
                Status = attrs?.Status ?? "error",
                Nsu = attrs?.Nsu
            };
        }
        catch
        {
            return new PagamentoResponse { Status = "error", Mensagem = "Erro ao processar pagamento com cartão." };
        }
    }

    public async Task<PagamentoResponse> CriarTransacaoPixAsync(
        decimal valor,
        string pedidoId,
        CancellationToken ct = default)
    {
        try
        {
            var accessToken = await auth.GetAccessTokenAsync("transactions", ct);
            var baseUrl = config["InfinitePay:BaseUrl"];
            var webhookBase = config["InfinitePay:WebhookBaseUrl"];
            var webhookSecret = config["InfinitePay:WebhookSecret"];

            var payload = new
            {
                amount = (int)(valor * 100),
                capture_method = "pix",
                metadata = new
                {
                    order_id = pedidoId,
                    callback = new
                    {
                        validate = $"{webhookBase}/api/webhook/pix/validar?pedido={pedidoId}",
                        confirm = $"{webhookBase}/api/webhook/pix/confirmar?pedido={pedidoId}",
                        secret = webhookSecret
                    }
                }
            };

            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await http.PostAsJsonAsync(
                $"{baseUrl}/v2/transactions",
                payload,
                ct);

            if (!response.IsSuccessStatusCode)
            {
                return new PagamentoResponse { Status = "error", Mensagem = "Falha ao criar transação PIX." };
            }

            var resultado = await response.Content.ReadFromJsonAsync<TransacaoApiResponse>(cancellationToken: ct);
            var attrs = resultado?.Data?.Attributes;

            return new PagamentoResponse
            {
                Status = attrs?.Status ?? "pending",
                Nsu = attrs?.Nsu,
                BrCode = attrs?.BrCode
            };
        }
        catch
        {
            return new PagamentoResponse { Status = "error", Mensagem = "Erro ao processar transação PIX." };
        }
    }

    public async Task<PagamentoResponse> ConsultarStatusAsync(
        string nsu,
        CancellationToken ct = default)
    {
        try
        {
            var accessToken = await auth.GetAccessTokenAsync("transactions", ct);
            var baseUrl = config["InfinitePay:BaseUrl"];

            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await http.GetAsync(
                $"{baseUrl}/v2/transactions/{nsu}",
                ct);

            if (!response.IsSuccessStatusCode)
            {
                return new PagamentoResponse { Status = "error", Mensagem = "Falha ao consultar status da transação." };
            }

            var resultado = await response.Content.ReadFromJsonAsync<TransacaoApiResponse>(cancellationToken: ct);
            var attrs = resultado?.Data?.Attributes;

            return new PagamentoResponse
            {
                Status = attrs?.Status ?? "error",
                Nsu = nsu,
                BrCode = attrs?.BrCode
            };
        }
        catch
        {
            return new PagamentoResponse { Status = "error", Mensagem = "Erro ao consultar status da transação." };
        }
    }
}
