using AssetValueAnalyzer.Application.Assets.Imports;
using AssetValueAnalyzer.Application.ProducerPriceIndices.Imports;
using AssetValueAnalyzer.Application.Reports.Calculation;
using AssetValueAnalyzer.Application.Reports.Creation;

namespace AssetValueAnalyzer.UnitTests.Reports;

public sealed class CreateFinancialImpactReportServiceTests
{
    [Fact]
    public async Task CreateAsync_WithoutDates_UsesAssetBoundsAndMatchesMonthlyInputs()
    {
        var rateReader = new FakeUsdCashChangeRateReader(
        [
            new(new DateOnly(2021, 12, 31), 10m),
            new(new DateOnly(2022, 1, 31), 20m),
            new(new DateOnly(2022, 2, 28), 30m)
        ]);
        var service = CreateService(rateReader);

        var result = await service.CreateAsync(
            new(CreateAssetValues(), CreateProducerPriceIndices()),
            CancellationToken.None);

        Assert.True(result.IsValid);
        var report = Assert.IsType<FinancialImpactReport>(result.Report);
        Assert.Equal(3, report.Rows.Count);
        Assert.Equal(new DateOnly(2021, 12, 1), report.Rows[0].Month);
        Assert.Equal(10m, report.Rows[0].UsdRate);
        Assert.Equal(100m, report.Rows[0].ProducerPriceIndex);
        Assert.Equal(new DateOnly(2022, 2, 1), report.Rows[^1].Month);
        Assert.Equal(30m, report.Rows[^1].UsdRate);
        Assert.Equal(new DateOnly(2021, 12, 21), rateReader.RequestedStartDate);
        Assert.Equal(new DateOnly(2022, 2, 28), rateReader.RequestedEndDate);
    }

    [Fact]
    public async Task CreateAsync_WithOnlyStartMonth_UsesLastAssetMonthAsEnd()
    {
        var rateReader = CreateCompleteRateReader();
        var service = CreateService(rateReader);

        var result = await service.CreateAsync(
            new(
                CreateAssetValues(),
                CreateProducerPriceIndices(),
                StartMonth: new DateOnly(2022, 1, 1)),
            CancellationToken.None);

        Assert.True(result.IsValid);
        var report = Assert.IsType<FinancialImpactReport>(result.Report);
        Assert.Equal(2, report.Rows.Count);
        Assert.Equal(new DateOnly(2022, 1, 1), report.Rows[0].Month);
        Assert.Equal(new DateOnly(2022, 2, 1), report.Rows[1].Month);
    }

    [Fact]
    public async Task CreateAsync_WithOnlyEndMonth_UsesFirstAssetMonthAsStart()
    {
        var rateReader = CreateCompleteRateReader();
        var service = CreateService(rateReader);

        var result = await service.CreateAsync(
            new(
                CreateAssetValues(),
                CreateProducerPriceIndices(),
                EndMonth: new DateOnly(2022, 1, 1)),
            CancellationToken.None);

        Assert.True(result.IsValid);
        var report = Assert.IsType<FinancialImpactReport>(result.Report);
        Assert.Equal(2, report.Rows.Count);
        Assert.Equal(new DateOnly(2021, 12, 1), report.Rows[0].Month);
        Assert.Equal(new DateOnly(2022, 1, 1), report.Rows[1].Month);
    }

    [Fact]
    public async Task CreateAsync_WhenMonthEndRateIsMissing_UsesPreviousWeekdayWithinTenDays()
    {
        MonthlyAssetValueInput[] assetValues =
        [
            new(new DateOnly(2022, 9, 1), 1_000m),
            new(new DateOnly(2022, 10, 1), 1_100m)
        ];
        MonthlyProducerPriceIndexInput[] indices =
        [
            new(new DateOnly(2022, 9, 1), 100m),
            new(new DateOnly(2022, 10, 1), 110m)
        ];
        var rateReader = new FakeUsdCashChangeRateReader(
        [
            new(new DateOnly(2022, 9, 29), 18m),
            new(new DateOnly(2022, 10, 30), 99m),
            new(new DateOnly(2022, 10, 28), 19m)
        ]);
        var service = CreateService(rateReader);

        var result = await service.CreateAsync(
            new(assetValues, indices),
            CancellationToken.None);

        Assert.True(result.IsValid);
        var report = Assert.IsType<FinancialImpactReport>(result.Report);
        Assert.Equal(18m, report.Rows[0].UsdRate);
        Assert.Equal(19m, report.Rows[1].UsdRate);
    }

    [Fact]
    public async Task CreateAsync_WithGapsBetweenAssetMonths_CalculatesAvailableMonths()
    {
        MonthlyAssetValueInput[] assetValues =
        [
            new(new DateOnly(2021, 12, 1), 1_000m),
            new(new DateOnly(2022, 2, 1), 1_200m)
        ];
        MonthlyProducerPriceIndexInput[] indices =
        [
            new(new DateOnly(2021, 12, 1), 100m),
            new(new DateOnly(2022, 2, 1), 150m)
        ];
        var rateReader = new FakeUsdCashChangeRateReader(
        [
            new(new DateOnly(2021, 12, 31), 10m),
            new(new DateOnly(2022, 2, 28), 30m)
        ]);
        var service = CreateService(rateReader);

        var result = await service.CreateAsync(
            new(assetValues, indices),
            CancellationToken.None);

        Assert.True(result.IsValid);
        var report = Assert.IsType<FinancialImpactReport>(result.Report);
        Assert.Equal(2, report.Rows.Count);
        Assert.Equal(new DateOnly(2021, 12, 1), report.Rows[0].Month);
        Assert.Equal(new DateOnly(2022, 2, 1), report.Rows[1].Month);
    }

    [Fact]
    public async Task CreateAsync_WithMissingIndex_ReturnsMonthSpecificErrorBeforeReadingRates()
    {
        var rateReader = CreateCompleteRateReader();
        var service = CreateService(rateReader);
        MonthlyProducerPriceIndexInput[] indices =
        [
            new(new DateOnly(2021, 12, 1), 100m),
            new(new DateOnly(2022, 2, 1), 150m)
        ];

        var result = await service.CreateAsync(
            new(CreateAssetValues(), indices),
            CancellationToken.None);

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal("MissingProducerPriceIndex", error.Code);
        Assert.Equal(new DateOnly(2022, 1, 1), error.Month);
        Assert.Contains("Ocak 2022", error.Message);
        Assert.Equal(0, rateReader.CallCount);
    }

    [Fact]
    public async Task CreateAsync_WithMissingRate_ReturnsMonthSpecificError()
    {
        var rateReader = new FakeUsdCashChangeRateReader(
        [
            new(new DateOnly(2021, 12, 31), 10m),
            new(new DateOnly(2022, 1, 31), 20m)
        ]);
        var service = CreateService(rateReader);

        var result = await service.CreateAsync(
            new(CreateAssetValues(), CreateProducerPriceIndices()),
            CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Null(result.Report);
        var error = Assert.Single(result.Errors);
        Assert.Equal("MissingUsdRate", error.Code);
        Assert.Equal(new DateOnly(2022, 2, 1), error.Month);
    }

    [Fact]
    public async Task CreateAsync_WithSameStartAndEndMonth_RequiresTwoReportRows()
    {
        var rateReader = CreateCompleteRateReader();
        var service = CreateService(rateReader);
        var selectedMonth = new DateOnly(2022, 1, 1);

        var result = await service.CreateAsync(
            new(
                CreateAssetValues(),
                CreateProducerPriceIndices(),
                selectedMonth,
                selectedMonth),
            CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal("AtLeastTwoMonthsRequired", Assert.Single(result.Errors).Code);
        Assert.Equal(0, rateReader.CallCount);
    }

    private static CreateFinancialImpactReportService CreateService(
        IUsdCashChangeRateReader rateReader) =>
        new(
            rateReader,
            new FinancialImpactReportRangeValidator(),
            new FinancialImpactCalculator());

    private static FakeUsdCashChangeRateReader CreateCompleteRateReader() =>
        new(
        [
            new(new DateOnly(2021, 12, 31), 10m),
            new(new DateOnly(2022, 1, 31), 20m),
            new(new DateOnly(2022, 2, 28), 30m)
        ]);

    private static MonthlyAssetValueInput[] CreateAssetValues() =>
    [
        new(new DateOnly(2021, 12, 1), 1_000m),
        new(new DateOnly(2022, 1, 1), 1_100m),
        new(new DateOnly(2022, 2, 1), 1_200m)
    ];

    private static MonthlyProducerPriceIndexInput[] CreateProducerPriceIndices() =>
    [
        new(new DateOnly(2021, 12, 1), 100m),
        new(new DateOnly(2022, 1, 1), 125m),
        new(new DateOnly(2022, 2, 1), 150m)
    ];

    private sealed class FakeUsdCashChangeRateReader(
        IReadOnlyList<UsdCashChangeRate> rates) : IUsdCashChangeRateReader
    {
        public int CallCount { get; private set; }

        public DateOnly? RequestedStartDate { get; private set; }

        public DateOnly? RequestedEndDate { get; private set; }

        public Task<IReadOnlyList<UsdCashChangeRate>> ReadAsync(
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            RequestedStartDate = startDate;
            RequestedEndDate = endDate;

            IReadOnlyList<UsdCashChangeRate> result = rates
                .Where(rate => rate.RateDate >= startDate && rate.RateDate <= endDate)
                .ToArray();

            return Task.FromResult(result);
        }
    }
}
