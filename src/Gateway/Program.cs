using Polly;
using System.Net.Http;
using ExchangeRate.Application.Services;
using ExchangeRate.Application.Providers;

var builder = WebApplication.CreateBuilder(args);

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Add services to the container.
builder.Services.AddControllers();
// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Application dependencies
builder.Services.AddScoped<IExchangeAggregatorService, ExchangeAggregatorService>();

// Http clients for providers with resiliency
builder.Services.AddHttpClient("api1")
    .AddPolicyHandler(Polly.Extensions.Http.HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(msg => (int)msg.StatusCode == 429)
        .RetryAsync(2));

builder.Services.AddHttpClient(
        "api2",
        c => c.DefaultRequestHeaders.Add("Accept", "application/xml"))
    .AddPolicyHandler(Polly.Extensions.Http.HttpPolicyExtensions
        .HandleTransientHttpError()
        .RetryAsync(2));

builder.Services.AddHttpClient("api3")
    .AddPolicyHandler(Polly.Extensions.Http.HttpPolicyExtensions
        .HandleTransientHttpError()
        .RetryAsync(2));

// Providers
builder.Services.AddScoped<IExchangeRateProvider, ExchangeRate.Gateway.Providers.Api1Provider>();
builder.Services.AddScoped<IExchangeRateProvider, ExchangeRate.Gateway.Providers.Api2Provider>();
builder.Services.AddScoped<IExchangeRateProvider, ExchangeRate.Gateway.Providers.Api3Provider>();

var app = builder.Build();

// Enable Swagger UI
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();
