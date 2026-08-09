using System.ComponentModel.DataAnnotations;

namespace AssetValueAnalyzer.Api.Features.ExchangeRates;

public sealed class GetExchangeRatesQuery : IValidatableObject
{
    [Required]
    public DateOnly? StartDate { get; init; }

    [Required]
    public DateOnly? EndDate { get; init; }

    [Range(0, int.MaxValue)]
    public int? BaseCurrencyCode { get; init; }

    [Range(0, int.MaxValue)]
    public int? ForeignCurrencyCode { get; init; }

    [Range(1, 200)]
    public int Limit { get; init; } = 100;

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (StartDate.HasValue &&
            EndDate.HasValue &&
            StartDate > EndDate)
        {
            yield return new ValidationResult(
                "StartDate cannot be later than EndDate.",
                [nameof(StartDate), nameof(EndDate)]);
        }
    }
}
