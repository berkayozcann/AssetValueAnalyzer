namespace AssetValueAnalyzer.Application.Reports.Calculation;

public sealed class FinancialImpactCalculator
{
    public FinancialImpactCalculationResult Calculate(
        IEnumerable<MonthlyFinancialInput> input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var periods = input
            .OrderBy(period => period.Month)
            .ToArray();
        var errors = Validate(periods);

        if (errors.Count > 0)
        {
            return FinancialImpactCalculationResult.Invalid(errors);
        }

        var reportPeriod = periods[^1];
        var calculatedPeriods = periods
            .Select(period => new CalculatedPeriod(
                period,
                DollarizedAmount: reportPeriod.UsdRate / period.UsdRate * period.AssetAmount,
                InflationAdjustedAmount:
                    reportPeriod.ProducerPriceIndex /
                    period.ProducerPriceIndex *
                    period.AssetAmount))
            .ToArray();
        var reportCalculatedPeriod = calculatedPeriods[^1];
        var rows = new List<FinancialImpactReportRow>(calculatedPeriods.Length);

        for (var index = 0; index < calculatedPeriods.Length; index++)
        {
            var current = calculatedPeriods[index];
            var previous = index == 0
                ? null
                : calculatedPeriods[index - 1];

            rows.Add(new FinancialImpactReportRow(
                current.Input.Month,
                current.Input.AssetAmount,
                CalculateMonthlyChangeRate(
                    current.Input.Month,
                    current.Input.AssetAmount,
                    previous?.Input.Month,
                    previous?.Input.AssetAmount),
                CalculateChangeRate(
                    reportPeriod.AssetAmount,
                    current.Input.AssetAmount),
                current.Input.UsdRate,
                current.DollarizedAmount,
                CalculateMonthlyChangeRate(
                    current.Input.Month,
                    current.DollarizedAmount,
                    previous?.Input.Month,
                    previous?.DollarizedAmount),
                CalculateChangeRate(
                    reportCalculatedPeriod.DollarizedAmount,
                    current.DollarizedAmount),
                CalculateChangeRate(
                    current.Input.AssetAmount,
                    current.DollarizedAmount),
                current.Input.ProducerPriceIndex,
                current.InflationAdjustedAmount,
                CalculateMonthlyChangeRate(
                    current.Input.Month,
                    current.InflationAdjustedAmount,
                    previous?.Input.Month,
                    previous?.InflationAdjustedAmount),
                CalculateChangeRate(
                    reportCalculatedPeriod.InflationAdjustedAmount,
                    current.InflationAdjustedAmount),
                CalculateChangeRate(
                    current.Input.AssetAmount,
                    current.InflationAdjustedAmount)));
        }

        var firstRow = rows[0];
        var report = new FinancialImpactReport(
            new FinancialImpactReportSummary(
                reportPeriod.Month,
                reportPeriod.AssetAmount,
                firstRow.AssetChangeRate,
                firstRow.DollarizedChangeRate,
                firstRow.InflationAdjustedChangeRate),
            rows);

        return FinancialImpactCalculationResult.Success(report);
    }

    private static List<FinancialImpactCalculationError> Validate(
        IReadOnlyList<MonthlyFinancialInput> periods)
    {
        var errors = new List<FinancialImpactCalculationError>();

        if (periods.Count < 2)
        {
            errors.Add(new(
                "AtLeastTwoMonthsRequired",
                "Finansal değişim hesabı için en az iki farklı aya ait veri gereklidir."));
        }

        foreach (var duplicateMonth in periods
                     .GroupBy(period => period.Month)
                     .Where(group => group.Count() > 1)
                     .Select(group => group.Key))
        {
            errors.Add(new(
                "DuplicateMonth",
                $"{duplicateMonth:yyyy-MM} ayı birden fazla kez gönderildi.",
                duplicateMonth));
        }

        foreach (var period in periods)
        {
            if (period.Month.Day != 1)
            {
                errors.Add(new(
                    "MonthMustBeNormalized",
                    $"{period.Month:yyyy-MM-dd} tarihi ayın ilk gününe normalize edilmelidir.",
                    period.Month));
            }

            if (period.AssetAmount < 0m)
            {
                errors.Add(new(
                    "NegativeAssetAmount",
                    $"{period.Month:yyyy-MM} ayındaki varlık tutarı negatif olamaz.",
                    period.Month));
            }

            if (period.UsdRate <= 0m)
            {
                errors.Add(new(
                    "InvalidUsdRate",
                    $"{period.Month:yyyy-MM} ayındaki USD kuru sıfırdan büyük olmalıdır.",
                    period.Month));
            }

            if (period.ProducerPriceIndex <= 0m)
            {
                errors.Add(new(
                    "InvalidProducerPriceIndex",
                    $"{period.Month:yyyy-MM} ayındaki Yİ-ÜFE endeksi sıfırdan büyük olmalıdır.",
                    period.Month));
            }
        }

        return errors;
    }

    private static decimal? CalculateChangeRate(
        decimal currentValue,
        decimal comparisonValue) =>
        comparisonValue == 0m
            ? null
            : (currentValue - comparisonValue) / comparisonValue;

    private static decimal? CalculateMonthlyChangeRate(
        DateOnly currentMonth,
        decimal currentValue,
        DateOnly? previousMonth,
        decimal? previousValue)
    {
        if (previousMonth is null || previousValue is null)
        {
            return 0m;
        }

        return previousMonth == currentMonth.AddMonths(-1)
            ? CalculateChangeRate(currentValue, previousValue.Value)
            : null;
    }

    private sealed record CalculatedPeriod(
        MonthlyFinancialInput Input,
        decimal DollarizedAmount,
        decimal InflationAdjustedAmount);
}
