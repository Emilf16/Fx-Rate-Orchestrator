using ExchangeRate.Domain.Models;

namespace ExchangeRate.Application.Providers;

public interface IExchangeRateProvider
{
    string Name { get; }
    Task<ExchangeQuote?> GetQuoteAsync(ExchangeRequest request, CancellationToken ct);
}
