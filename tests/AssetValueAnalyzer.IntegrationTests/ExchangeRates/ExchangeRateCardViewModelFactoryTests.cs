using AssetValueAnalyzer.Application.ExchangeRates.Queries;
using AssetValueAnalyzer.Web.Features.Shared;

namespace AssetValueAnalyzer.IntegrationTests.ExchangeRates;

public sealed class ExchangeRateCardViewModelFactoryTests
{
    [Fact]
    public void Create_WithoutRate_ReturnsExplicitEmptyState()
    {
        var viewModel = ExchangeRateCardViewModelFactory.Create(null);

        Assert.Equal("USD / TRY", viewModel.Label);
        Assert.Equal("—", viewModel.FormattedRate);
        Assert.Equal("Kur verisi henüz alınmadı.", viewModel.LastSyncText);
        Assert.Equal(ExchangeRateTrend.Unavailable, viewModel.Trend);
        Assert.False(viewModel.HasRate);
    }

    [Fact]
    public void Create_WithFirstRate_KeepsValueAvailableWithoutInventingTrend()
    {
        var retrievedAtUtc = new DateTimeOffset(2026, 8, 10, 0, 30, 0, TimeSpan.Zero);
        var viewModel = ExchangeRateCardViewModelFactory.Create(
            new CurrentUsdExchangeRate(
                45.8708m,
                new DateOnly(2026, 8, 10),
                retrievedAtUtc,
                PreviousValue: null));

        Assert.Equal("45,8708", viewModel.FormattedRate);
        Assert.Equal(
            "Karşılaştırma için önceki kur verisi bulunmuyor.",
            viewModel.TrendText);
        Assert.Equal(
            $"Son kontrol · {retrievedAtUtc.ToLocalTime():dd.MM.yyyy HH:mm}",
            viewModel.LastSyncText);
        Assert.Equal(ExchangeRateTrend.Unavailable, viewModel.Trend);
        Assert.True(viewModel.HasRate);
    }

    [Theory]
    [InlineData(11, 10, ExchangeRateTrend.Increased, "USD/TRY kuru bir önceki kur gününe göre yükseldi.")]
    [InlineData(9, 10, ExchangeRateTrend.Decreased, "USD/TRY kuru bir önceki kur gününe göre düştü.")]
    [InlineData(10, 10, ExchangeRateTrend.Unchanged, "USD/TRY kuru bir önceki kur gününe göre değişmedi.")]
    public void Create_WithComparableRates_MapsDirectionAndAccessibleDescription(
        int currentValue,
        int previousValue,
        ExchangeRateTrend expectedTrend,
        string expectedTrendText)
    {
        var viewModel = ExchangeRateCardViewModelFactory.Create(
            new CurrentUsdExchangeRate(
                currentValue,
                new DateOnly(2026, 8, 10),
                new DateTimeOffset(2026, 8, 10, 0, 30, 0, TimeSpan.Zero),
                previousValue));

        Assert.Equal(expectedTrend, viewModel.Trend);
        Assert.Equal(expectedTrendText, viewModel.TrendText);
        Assert.True(viewModel.HasRate);
    }
}
