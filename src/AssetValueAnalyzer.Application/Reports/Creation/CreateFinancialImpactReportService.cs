using AssetValueAnalyzer.Application.Assets.Imports;
using AssetValueAnalyzer.Application.ProducerPriceIndices.Imports;
using AssetValueAnalyzer.Application.Reports.Calculation;

namespace AssetValueAnalyzer.Application.Reports.Creation;

public sealed class CreateFinancialImpactReportService(
    IUsdCashChangeRateReader rateReader,
    FinancialImpactReportRangeValidator rangeValidator,
    FinancialImpactCalculator calculator)
{
    private const int MaximumRateLookbackDays = 10;

    public async Task<FinancialImpactReportCreationResult> CreateAsync(
        CreateFinancialImpactReportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var rangeValidation = rangeValidator.Validate(request);

        if (!rangeValidation.IsValid)
        {
            return FinancialImpactReportCreationResult.Invalid(rangeValidation.Errors);
        }

        var producerPriceIndices = request.ProducerPriceIndices
            .OrderBy(value => value.Month)
            .ToArray();
        var selectedAssetValues = rangeValidation.SelectedAssetValues;

        var indicesByMonth = producerPriceIndices.ToDictionary(
            value => value.Month,
            value => value.Value);
        var targetRateDates = selectedAssetValues
            .Select(asset => new
            {
                asset.Month,
                LastBusinessDay = GetLastWeekdayOfMonth(asset.Month)
            })
            .ToArray();
        var firstRateSearchDate = targetRateDates
            .Min(value => value.LastBusinessDay)
            .AddDays(-MaximumRateLookbackDays);
        var lastRateSearchDate = targetRateDates
            .Max(value => value.LastBusinessDay);
        var availableRates = await rateReader.ReadAsync(
            firstRateSearchDate,
            lastRateSearchDate,
            cancellationToken);
        var ratesByMonth = new Dictionary<DateOnly, decimal>();
        var missingRateErrors = new List<FinancialImpactReportCreationError>();

        foreach (var target in targetRateDates)
        {
            var earliestAllowedDate = target.LastBusinessDay
                .AddDays(-MaximumRateLookbackDays);
            var rate = availableRates
                .Where(candidate =>
                    candidate.RateDate >= earliestAllowedDate &&
                    candidate.RateDate <= target.LastBusinessDay &&
                    IsWeekday(candidate.RateDate))
                .OrderByDescending(candidate => candidate.RateDate)
                .FirstOrDefault();

            if (rate is null)
            {
                missingRateErrors.Add(new(
                    "MissingUsdRate",
                    $"{target.Month:yyyy-MM} ayının son iş günü ve önceki {MaximumRateLookbackDays} gün içinde USD/TRY kuru bulunamadı.",
                    target.Month));
                continue;
            }

            ratesByMonth[target.Month] = rate.Value;
        }

        if (missingRateErrors.Count > 0)
        {
            return FinancialImpactReportCreationResult.Invalid(missingRateErrors);
        }

        var calculationInput = selectedAssetValues
            .Select(asset => new MonthlyFinancialInput(
                asset.Month,
                asset.Amount,
                ratesByMonth[asset.Month],
                indicesByMonth[asset.Month]))
            .ToArray();
        var calculationResult = calculator.Calculate(calculationInput);

        if (!calculationResult.IsValid)
        {
            return FinancialImpactReportCreationResult.Invalid(
                calculationResult.Errors
                    .Select(error => new FinancialImpactReportCreationError(
                        error.Code,
                        error.Message,
                        error.Month))
                    .ToArray());
        }

        return FinancialImpactReportCreationResult.Success(calculationResult.Report!);
    }

    private static DateOnly GetLastWeekdayOfMonth(DateOnly month)
    {
        var lastDay = new DateOnly(
            month.Year,
            month.Month,
            DateTime.DaysInMonth(month.Year, month.Month));

        return lastDay.DayOfWeek switch
        {
            DayOfWeek.Saturday => lastDay.AddDays(-1),
            DayOfWeek.Sunday => lastDay.AddDays(-2),
            _ => lastDay
        };
    }

    private static bool IsWeekday(DateOnly date) =>
        date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
}
