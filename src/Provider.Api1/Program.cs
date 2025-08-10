var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapPost("/rate", (Api1Request req) =>
{
    if (string.IsNullOrWhiteSpace(req.from) || string.IsNullOrWhiteSpace(req.to) || req.value <= 0)
        return Results.BadRequest(new { error = "Invalid input" });

    // Randomized rate per request to avoid deterministic ordering between providers
    var rate = 4.8m + (decimal)Random.Shared.NextDouble() * 1.6m; // 4.8 .. 6.4
    var rounded = Math.Round(rate, 2, MidpointRounding.AwayFromZero);
    return Results.Ok(new { rate = rounded });
});

app.Run();

public record Api1Request(string from, string to, decimal value);
