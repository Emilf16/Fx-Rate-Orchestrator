using System.Text;
using System.Xml.Linq;
using ExchangeRate.Application.Providers;
using ExchangeRate.Domain.Models;

namespace ExchangeRate.Gateway.Providers;

public sealed class Api2Provider(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<Api2Provider> logger) : IExchangeRateProvider
{
    public string Name => "API2";

    public async Task<ExchangeQuote?> GetQuoteAsync(ExchangeRequest request, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("api2");
        var baseUrl = config["Providers:Api2:BaseUrl"] ?? "http://provider2:5002";
        var url = $"{baseUrl}/rate";

        var xml = new XDocument(
            new XElement("XML",
                new XElement("From", request.SourceCurrency),
                new XElement("To", request.TargetCurrency),
                new XElement("Amount", request.Amount)
            ));
        var content = new StringContent(xml.ToString(), Encoding.UTF8, "application/xml");

        using var resp = await client.PostAsync(url, content, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            logger.LogWarning("{Provider} returned non-success status {Status}", Name, (int)resp.StatusCode);
            return null;
        }

        var str = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var response = XDocument.Parse(str);
        var resultEl = response.Root?.Element("Result");
        if (resultEl == null || !decimal.TryParse(resultEl.Value, out var total)) return null;
        var rate = total / (request.Amount == 0 ? 1 : request.Amount);
        // Return raw values; rounding/formatting is handled by the Gateway controller.
        return new ExchangeQuote(Name, rate, total);
    }
}
