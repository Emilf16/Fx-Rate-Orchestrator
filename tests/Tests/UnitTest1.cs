using ExchangeRate.Application.Services;
using ExchangeRate.Application.Providers;
using ExchangeRate.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace ExchangeRate.Tests;

public class AggregatorTests
{
    private static IExchangeAggregatorService CreateService(params ExchangeQuote?[] quotes)
    {
        var providers = new List<IExchangeRateProvider>();
        int i = 0;
        foreach (var q in quotes)
        {
            var mock = new Mock<IExchangeRateProvider>();
            mock.SetupGet(m => m.Name).Returns($"P{i}");
            mock.Setup(m => m.GetQuoteAsync(It.IsAny<ExchangeRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(q);
            providers.Add(mock.Object);
            i++;
        }
        var logger = Mock.Of<ILogger<ExchangeAggregatorService>>();
        return new ExchangeAggregatorService(logger, providers);
    }

    [Fact]
    public async Task Returns_best_quote_among_valid()
    {
        var svc = CreateService(
            new ExchangeQuote("A", 5, 500),
            new ExchangeQuote("B", 6, 600),
            null
        );
        var req = new ExchangeRequest("USD", "EUR", 100);
        var res = await svc.GetBestQuoteAsync(req, TimeSpan.FromSeconds(2), CancellationToken.None);
        res.ConvertedAmount.Should().Be(600);
        res.Provider.Should().Be("B");
    }

    [Fact]
    public async Task Throws_if_all_invalid_or_unavailable()
    {
        var providers = new List<IExchangeRateProvider>();
        var failing = new Mock<IExchangeRateProvider>();
        failing.SetupGet(m => m.Name).Returns("F");
        failing.Setup(m => m.GetQuoteAsync(It.IsAny<ExchangeRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException());
        providers.Add(failing.Object);
        var logger = Mock.Of<ILogger<ExchangeAggregatorService>>();
        var svc = new ExchangeAggregatorService(logger, providers);
        var req = new ExchangeRequest("USD", "EUR", 100);
        await FluentActions.Awaiting(() => svc.GetBestQuoteAsync(req, TimeSpan.FromMilliseconds(100), CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Uses_timeout_to_cancel_slow_providers()
    {
        var slow = new Mock<IExchangeRateProvider>();
        slow.SetupGet(m => m.Name).Returns("Slow");
        slow.Setup(m => m.GetQuoteAsync(It.IsAny<ExchangeRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async () => { await Task.Delay(2000); return new ExchangeQuote("Slow", 10, 1000); });

        var fast = new Mock<IExchangeRateProvider>();
        fast.SetupGet(m => m.Name).Returns("Fast");
        fast.Setup(m => m.GetQuoteAsync(It.IsAny<ExchangeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExchangeQuote("Fast", 5, 500));

        var logger = Mock.Of<ILogger<ExchangeAggregatorService>>();
        var svc = new ExchangeAggregatorService(logger, new[] { slow.Object, fast.Object });
        var req = new ExchangeRequest("USD", "EUR", 100);
        var res = await svc.GetBestQuoteAsync(req, TimeSpan.FromMilliseconds(300), CancellationToken.None);
        res.Provider.Should().Be("Fast");
        res.ConvertedAmount.Should().Be(500);
    }

    [Theory]
    [InlineData(null, "EUR", 100)]
    [InlineData("US", "EUR", 100)]
    [InlineData("USD", null, 100)]
    [InlineData("USD", "E", 100)]
    [InlineData("USD", "EUR", 0)]
    public async Task Validates_input(string? from, string? to, decimal amount)
    {
        var svc = CreateService(new ExchangeQuote("A", 1, 1));
        var req = new ExchangeRequest(from ?? string.Empty, to ?? string.Empty, amount);
        await FluentActions.Awaiting(() => svc.GetBestQuoteAsync(req, TimeSpan.FromSeconds(1), CancellationToken.None))
            .Should().ThrowAsync<ArgumentException>();
    }
}
