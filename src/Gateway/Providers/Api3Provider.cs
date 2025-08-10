using System.Net.Http.Json;
using ExchangeRate.Application.Providers;
using ExchangeRate.Domain.Models;

namespace ExchangeRate.Gateway.Providers;

public sealed class Api3Provider(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<Api3Provider> logger) : IExchangeRateProvider
{
    public string Name => "API3";

    public async Task<ExchangeQuote?> GetQuoteAsync(ExchangeRequest request, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("api3");
        var baseUrl = config["Providers:Api3:BaseUrl"] ?? "http://provider3:5003";
        var url = $"{baseUrl}/rate";

        var payload = new { exchange = new { sourceCurrency = request.SourceCurrency, targetCurrency = request.TargetCurrency, quantity = request.Amount } };
        using var resp = await client.PostAsJsonAsync(url, payload, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            logger.LogWarning("{Provider} returned non-success status {Status}", Name, (int)resp.StatusCode);
            return null;
        }
        var data = await resp.Content.ReadFromJsonAsync<Api3Response>(cancellationToken: ct).ConfigureAwait(false);
        if (data is null || data.statusCode != 200 || data.data is null) return null;
        var total = data.data.total;
        var rate = total / (request.Amount == 0 ? 1 : request.Amount);
        // Return raw values; rounding/formatting is handled by the Gateway controller.
        return new ExchangeQuote(Name, rate, total);
    }

    private sealed record Api3Response(int statusCode, string? message, Api3Data? data);
    private sealed record Api3Data(decimal total);
}
