namespace ExchangeRate.Domain.Models;

public sealed record ExchangeQuote(
    string Provider,
    decimal Rate,
    decimal ConvertedAmount
);
