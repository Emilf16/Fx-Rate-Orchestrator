namespace ExchangeRate.Domain.Models;

public sealed record ExchangeRequest(
    string SourceCurrency,
    string TargetCurrency,
    decimal Amount
)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SourceCurrency) || SourceCurrency.Length != 3)
            throw new ArgumentException("Invalid source currency");
        if (string.IsNullOrWhiteSpace(TargetCurrency) || TargetCurrency.Length != 3)
            throw new ArgumentException("Invalid target currency");
        if (Amount <= 0)
            throw new ArgumentException("Amount must be greater than zero");
    }
}
