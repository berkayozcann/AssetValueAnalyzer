using AssetValueAnalyzer.Application.Reports.Calculation;

namespace AssetValueAnalyzer.UnitTests.Reports;

public sealed class FinancialImpactCalculatorTests
{
    private readonly FinancialImpactCalculator _calculator = new();

    [Fact]
    public void Calculate_WithControlledCashChangeRateFixture_ReturnsAllFourteenValues()
    {
        MonthlyFinancialInput[] input =
        [
            new(new DateOnly(2021, 12, 1), 1_000m, 10m, 100m),
            new(new DateOnly(2022, 1, 1), 1_100m, 20m, 125m),
            new(new DateOnly(2022, 2, 1), 1_200m, 30m, 150m)
        ];

        var result = _calculator.Calculate(input);

        Assert.True(result.IsValid);
        var report = Assert.IsType<FinancialImpactReport>(result.Report);
        var firstRow = report.Rows[0];
        var secondRow = report.Rows[1];
        var reportRow = report.Rows[2];

        Assert.Equal(new DateOnly(2022, 2, 1), report.Summary.ReportMonth);
        Assert.Equal(1_200m, report.Summary.ReportMonthAssetAmount);
        Assert.Equal(0.2m, report.Summary.NominalAssetChangeRate);
        Assert.Equal(-0.6m, report.Summary.DollarizedAssetChangeRate);
        Assert.Equal(-0.2m, report.Summary.InflationAdjustedAssetChangeRate);

        Assert.Equal(1_000m, firstRow.AssetAmount);
        Assert.Equal(0m, firstRow.MonthlyAssetChangeRate);
        Assert.Equal(0.2m, firstRow.AssetChangeRate);
        Assert.Equal(10m, firstRow.UsdRate);
        Assert.Equal(3_000m, firstRow.DollarizedAmount);
        Assert.Equal(0m, firstRow.MonthlyDollarizedChangeRate);
        Assert.Equal(-0.6m, firstRow.DollarizedChangeRate);
        AssertClose(-2m / 3m, firstRow.DollarizationEffectRate);
        Assert.Equal(100m, firstRow.ProducerPriceIndex);
        Assert.Equal(1_500m, firstRow.InflationAdjustedAmount);
        Assert.Equal(0m, firstRow.MonthlyInflationAdjustedChangeRate);
        Assert.Equal(-0.2m, firstRow.InflationAdjustedChangeRate);
        AssertClose(-1m / 3m, firstRow.InflationEffectRate);

        Assert.Equal(0.1m, secondRow.MonthlyAssetChangeRate);
        Assert.Equal(1_650m, secondRow.DollarizedAmount);
        Assert.Equal(-0.45m, secondRow.MonthlyDollarizedChangeRate);
        Assert.Equal(1_320m, secondRow.InflationAdjustedAmount);
        Assert.Equal(-0.12m, secondRow.MonthlyInflationAdjustedChangeRate);

        Assert.Equal(0m, reportRow.AssetChangeRate);
        Assert.Equal(0m, reportRow.DollarizedChangeRate);
        Assert.Equal(0m, reportRow.DollarizationEffectRate);
        Assert.Equal(0m, reportRow.InflationAdjustedChangeRate);
        Assert.Equal(0m, reportRow.InflationEffectRate);
    }

    [Fact]
    public void Calculate_WithReferenceChangeRateFixture_MatchesExcelFormulaOutputs()
    {
        var input = CreateReferenceChangeRateFixture();

        var result = _calculator.Calculate(input);

        Assert.True(result.IsValid);
        var report = Assert.IsType<FinancialImpactReport>(result.Report);
        var firstRow = report.Rows[0];
        var januaryRow = report.Rows[1];
        var reportRow = report.Rows[^1];

        Assert.Equal(21, report.Rows.Count);
        Assert.Equal(0.625m, firstRow.AssetChangeRate);
        AssertClose(
            2_625_666.15620214m,
            firstRow.DollarizedAmount,
            tolerance: 0.000001m);
        AssertClose(-0.207820082120194m, firstRow.DollarizedChangeRate);
        AssertClose(-0.512504665920119m, firstRow.DollarizationEffectRate);
        AssertClose(
            3_258_744.14282221m,
            firstRow.InflationAdjustedAmount,
            tolerance: 0.000001m);
        AssertClose(-0.361717303096206m, firstRow.InflationAdjustedChangeRate);
        AssertClose(-0.607210648059204m, firstRow.InflationEffectRate);

        Assert.Equal(0.03125m, januaryRow.MonthlyAssetChangeRate);
        AssertClose(0.0156957013574662m, januaryRow.MonthlyDollarizedChangeRate);
        AssertClose(-0.0662822843502829m, januaryRow.MonthlyInflationAdjustedChangeRate);

        Assert.Equal(2_080_000m, reportRow.AssetAmount);
        Assert.Equal(2_080_000m, reportRow.DollarizedAmount);
        Assert.Equal(2_080_000m, reportRow.InflationAdjustedAmount);
        Assert.Equal(0m, reportRow.AssetChangeRate);
        Assert.Equal(0m, reportRow.DollarizedChangeRate);
        Assert.Equal(0m, reportRow.DollarizationEffectRate);
        Assert.Equal(0m, reportRow.InflationAdjustedChangeRate);
        Assert.Equal(0m, reportRow.InflationEffectRate);
    }

    [Fact]
    public void Calculate_WithUnorderedInput_SortsRowsByMonth()
    {
        MonthlyFinancialInput[] input =
        [
            new(new DateOnly(2022, 2, 1), 1_200m, 30m, 150m),
            new(new DateOnly(2021, 12, 1), 1_000m, 10m, 100m)
        ];

        var result = _calculator.Calculate(input);

        var report = Assert.IsType<FinancialImpactReport>(result.Report);
        Assert.Equal(new DateOnly(2021, 12, 1), report.Rows[0].Month);
        Assert.Equal(new DateOnly(2022, 2, 1), report.Rows[1].Month);
    }

    [Fact]
    public void Calculate_WithZeroAssetAmount_LeavesUndefinedRatiosEmpty()
    {
        MonthlyFinancialInput[] input =
        [
            new(new DateOnly(2021, 12, 1), 0m, 10m, 100m),
            new(new DateOnly(2022, 1, 1), 1_000m, 20m, 125m)
        ];

        var result = _calculator.Calculate(input);

        Assert.True(result.IsValid);
        var report = Assert.IsType<FinancialImpactReport>(result.Report);
        var firstRow = report.Rows[0];
        var secondRow = report.Rows[1];

        Assert.Null(firstRow.AssetChangeRate);
        Assert.Null(firstRow.DollarizedChangeRate);
        Assert.Null(firstRow.DollarizationEffectRate);
        Assert.Null(firstRow.InflationAdjustedChangeRate);
        Assert.Null(firstRow.InflationEffectRate);
        Assert.Null(secondRow.MonthlyAssetChangeRate);
        Assert.Null(secondRow.MonthlyDollarizedChangeRate);
        Assert.Null(secondRow.MonthlyInflationAdjustedChangeRate);
    }

    [Fact]
    public void Calculate_WithDuplicateMonth_ReturnsValidationError()
    {
        MonthlyFinancialInput[] input =
        [
            new(new DateOnly(2021, 12, 1), 1_000m, 10m, 100m),
            new(new DateOnly(2021, 12, 1), 1_100m, 11m, 101m)
        ];

        var result = _calculator.Calculate(input);

        Assert.False(result.IsValid);
        Assert.Null(result.Report);
        Assert.Contains(result.Errors, error => error.Code == "DuplicateMonth");
    }

    [Fact]
    public void Calculate_WithOnlyOneMonth_ReturnsValidationError()
    {
        MonthlyFinancialInput[] input =
        [
            new(new DateOnly(2021, 12, 1), 1_000m, 10m, 100m)
        ];

        var result = _calculator.Calculate(input);

        Assert.False(result.IsValid);
        Assert.Equal("AtLeastTwoMonthsRequired", Assert.Single(result.Errors).Code);
    }

    private static MonthlyFinancialInput[] CreateReferenceChangeRateFixture()
    {
        decimal[] usdChangeRates =
        [
            13.06m, 13.26m, 13.70m, 14.49m, 14.65m, 16.25m, 16.47m,
            17.64m, 17.97m, 18.28m, 18.40m, 18.42m, 18.48m, 18.60m,
            18.67m, 18.96m, 19.42m, 20.69m, 23.37m, 25.45m, 26.79m
        ];
        decimal[] producerPriceIndices =
        [
            1022.25m, 1129.03m, 1210.60m, 1321.90m, 1423.27m, 1548.01m,
            1652.75m, 1738.21m, 1780.05m, 1865.09m, 2011.13m, 2026.08m,
            2021.19m, 2105.17m, 2138.04m, 2147.44m, 2164.94m, 2179.02m,
            2320.72m, 2511.75m, 2602.54m
        ];

        return Enumerable.Range(0, 21)
            .Select(index => new MonthlyFinancialInput(
                new DateOnly(2021, 12, 1).AddMonths(index),
                1_280_000m + (40_000m * index),
                usdChangeRates[index],
                producerPriceIndices[index]))
            .ToArray();
    }

    private static void AssertClose(
        decimal expected,
        decimal? actual,
        decimal tolerance = 0.000000001m)
    {
        var value = Assert.IsType<decimal>(actual);
        Assert.InRange(value, expected - tolerance, expected + tolerance);
    }
}
