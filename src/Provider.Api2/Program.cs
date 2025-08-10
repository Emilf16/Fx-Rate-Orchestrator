using System.Xml.Linq;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapPost("/rate", async (HttpRequest request) =>
{
    using var reader = new StreamReader(request.Body, Encoding.UTF8);
    var body = await reader.ReadToEndAsync();
    try
    {
        var xml = XDocument.Parse(body);
        var from = xml.Root?.Element("From")?.Value;
        var to = xml.Root?.Element("To")?.Value;
        var amountStr = xml.Root?.Element("Amount")?.Value;
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to) || !decimal.TryParse(amountStr, out var amount) || amount <= 0)
        {
            return Results.BadRequest("<XML><Error>Invalid input</Error></XML>");
        }
        // Randomized rate per request
        var rate = 4.9m + (decimal)Random.Shared.NextDouble() * 1.5m; // 4.9 .. 6.4
        var total = Math.Round(rate * amount, 2, MidpointRounding.AwayFromZero);
        var response = new XDocument(new XElement("XML", new XElement("Result", total)));
        return Results.Content(response.ToString(), "application/xml");
    }
    catch
    {
        return Results.BadRequest("<XML><Error>Invalid XML</Error></XML>");
    }
});

app.Run();

