using System.Collections.Concurrent;
using ExchangeRate.Application.Providers;
using ExchangeRate.Domain.Models;
using Microsoft.Extensions.Logging;

namespace ExchangeRate.Application.Services;

public interface IExchangeAggregatorService
{
    Task<BestQuoteResult> GetBestQuoteAsync(ExchangeRequest request, TimeSpan timeout, CancellationToken ct);
}

public sealed class ExchangeAggregatorService(ILogger<ExchangeAggregatorService> logger, IEnumerable<IExchangeRateProvider> providers)
    : IExchangeAggregatorService
{
    private readonly ILogger<ExchangeAggregatorService> _logger = logger;
    private readonly IReadOnlyList<IExchangeRateProvider> _providers = providers.ToList();

    public async Task<BestQuoteResult> GetBestQuoteAsync(ExchangeRequest request, TimeSpan timeout, CancellationToken ct)
    {
        request.Validate();

        if (_providers.Count == 0)
            throw new InvalidOperationException("No providers configured");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        var results = new ConcurrentBag<ExchangeQuote>();

        var tasks = _providers.Select(async p =>
        {
            try
            {
                var quote = await p.GetQuoteAsync(request, cts.Token).ConfigureAwait(false);
                if (quote is not null && quote.Rate > 0 && quote.ConvertedAmount > 0)
                {
                    results.Add(quote);
                }
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                _logger.LogWarning("Provider {Provider} cancelled due to timeout", p.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Provider {Provider} failed", p.Name);
            }
        }).ToArray();

        // Wait until either all providers complete or the timeout elapses
        var allTask = Task.WhenAll(tasks);
        try
        {
            await Task.WhenAny(allTask, Task.Delay(Timeout.Infinite, cts.Token)).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // expected when timeout hits
        }

        // Pick the best among whatever we have so far
        var best = results.OrderByDescending(r => r.ConvertedAmount).FirstOrDefault();
        if (best is null)
            throw new InvalidOperationException("No valid quotes received");

        return new BestQuoteResult(best.Provider, best.Rate, best.ConvertedAmount);
    }
}
