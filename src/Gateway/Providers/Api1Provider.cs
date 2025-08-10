using System.Net.Http.Json;
using ExchangeRate.Application.Providers;
using ExchangeRate.Domain.Models;

namespace ExchangeRate.Gateway.Providers;

public sealed class Api1Provider(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<Api1Provider> logger) : IExchangeRateProvider
{
    public string Name => "API1";

    public async Task<ExchangeQuote?> GetQuoteAsync(ExchangeRequest request, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("api1");
        var baseUrl = config["Providers:Api1:BaseUrl"] ?? "http://provider1:5001";
        var url = $"{baseUrl}/rate";

        var payload = new { from = request.SourceCurrency, to = request.TargetCurrency, value = request.Amount };
        using var resp = await client.PostAsJsonAsync(url, payload, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            logger.LogWarning("{Provider} returned non-success status {Status}", Name, (int)resp.StatusCode);
            return null;
        }
        var data = await resp.Content.ReadFromJsonAsync<Api1Response>(cancellationToken: ct).ConfigureAwait(false);
        if (data is null || data.rate <= 0) return null;
        var converted = data.rate * request.Amount;
        // Return raw values; rounding/formatting is handled by the Gateway controller.
        return new ExchangeQuote(Name, data.rate, converted);
    }

    private sealed record Api1Response(decimal rate);
}
