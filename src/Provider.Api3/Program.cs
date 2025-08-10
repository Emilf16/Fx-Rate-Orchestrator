var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapPost("/rate", (Api3Request req) =>
{
    if (req.exchange is null || string.IsNullOrWhiteSpace(req.exchange.sourceCurrency) || string.IsNullOrWhiteSpace(req.exchange.targetCurrency) || req.exchange.quantity <= 0)
        return Results.BadRequest(new { statusCode = 400, message = "Invalid input" });

    var amount = req.exchange.quantity;

    // Randomized rate per request
    var rate = 5.0m + (decimal)Random.Shared.NextDouble() * 1.4m; // 5.0 .. 6.4
    var total = Math.Round(rate * amount, 2, MidpointRounding.AwayFromZero);
    return Results.Ok(new { statusCode = 200, message = "OK", data = new { total } });
});

app.Run();

public record Api3Exchange(string sourceCurrency, string targetCurrency, decimal quantity);
public record Api3Request(Api3Exchange? exchange);
