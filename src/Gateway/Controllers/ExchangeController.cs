using System.ComponentModel.DataAnnotations;
using ExchangeRate.Application.Services;
using ExchangeRate.Domain.Models;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace ExchangeRate.Gateway.Controllers;

[ApiController]
[Route("api/exchange")]
public class ExchangeController(IExchangeAggregatorService service, ILogger<ExchangeController> logger) : ControllerBase
{
    [HttpPost("best-quote")]
    public async Task<IActionResult> GetBestQuote([FromBody] ExchangeRequestDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            var req = new ExchangeRequest(dto.SourceCurrency!, dto.TargetCurrency!, dto.Amount);
            var result = await service.GetBestQuoteAsync(req, TimeSpan.FromSeconds(3), ct);
            var roundedRate = Math.Round(result.Rate, 2, MidpointRounding.AwayFromZero);
            var roundedTotal = Math.Round(result.ConvertedAmount, 2, MidpointRounding.AwayFromZero);
            // Return as numbers (decimals). Note: JSON does not preserve trailing zeros.
            return Ok(new { provider = result.Provider, rate = roundedRate, total = roundedTotal });
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid input");
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Business rule error");
            return StatusCode(503, new { error = ex.Message });
        }
        catch (OperationCanceledException)
        {
            return StatusCode(504, new { error = "Timeout retrieving quotes" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error");
            return StatusCode(500, new { error = "Unexpected error" });
        }
    }
}

public sealed class ExchangeRequestDto
{
    [Required, StringLength(3, MinimumLength = 3)]
    public string? SourceCurrency { get; set; }

    [Required, StringLength(3, MinimumLength = 3)]
    public string? TargetCurrency { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }
}
