namespace ExchangeRate.Domain.Models;

public sealed record BestQuoteResult(
    string Provider,
    decimal Rate,
    decimal ConvertedAmount
);
