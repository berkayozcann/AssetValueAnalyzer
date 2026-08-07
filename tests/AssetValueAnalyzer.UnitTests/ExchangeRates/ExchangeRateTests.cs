using AssetValueAnalyzer.Domain.ExchangeRates;

namespace AssetValueAnalyzer.UnitTests.ExchangeRates;

public sealed class ExchangeRateTests
{
    [Fact]
    public void Constructor_CreatesRateWithSourceDateAndUtcRetrievalTime()
    {
        var sourceUpdatedAt = new DateTime(2026, 8, 7, 9, 30, 0);
        var retrievedAt = new DateTimeOffset(2026, 8, 7, 10, 0, 0, TimeSpan.FromHours(3));

        var rate = CreateRate(sourceUpdatedAt, retrievedAt, cashChangeRate: 46.55073m);

        Assert.Equal(1, rate.BaseCurrencyCode);
        Assert.Equal(56, rate.ForeignCurrencyCode);
        Assert.Equal(new DateOnly(2026, 8, 7), rate.RateDate);
        Assert.Equal(46.55073m, rate.CashChangeRate);
        Assert.Equal(TimeSpan.Zero, rate.RetrievedAtUtc.Offset);
        Assert.Equal(7, rate.RetrievedAtUtc.Hour);
    }

    [Fact]
    public void Constructor_RejectsZeroCashChangeRate()
    {
        var action = () => CreateRate(
            new DateTime(2026, 8, 7, 9, 30, 0),
            DateTimeOffset.UtcNow,
            cashChangeRate: 0m);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(action);

        Assert.Equal("cashChangeRate", exception.ParamName);
    }

    [Fact]
    public void Constructor_AcceptsZeroForOptionalSourceRates()
    {
        var rate = new ExchangeRate(
            baseCurrencyCode: 1,
            foreignCurrencyCode: 56,
            sourceUpdatedAt: new DateTime(2021, 12, 8),
            retrievedAtUtc: DateTimeOffset.UtcNow,
            changeRate: 0m,
            exchangeRateValue: 0m,
            cashChangeRate: 13.14350m,
            cashExchangeRate: 0m,
            centralBankChangeRate: 0m,
            centralBankExchangeRate: 0m,
            crossRate: 0m);

        Assert.Equal(13.14350m, rate.CashChangeRate);
        Assert.Equal(0m, rate.CrossRate);
    }

    [Fact]
    public void Constructor_RejectsNegativeOptionalSourceRate()
    {
        var action = () => new ExchangeRate(
            baseCurrencyCode: 1,
            foreignCurrencyCode: 56,
            sourceUpdatedAt: new DateTime(2021, 12, 8),
            retrievedAtUtc: DateTimeOffset.UtcNow,
            changeRate: -1m,
            exchangeRateValue: 0m,
            cashChangeRate: 13.14350m,
            cashExchangeRate: 0m,
            centralBankChangeRate: 0m,
            centralBankExchangeRate: 0m,
            crossRate: 0m);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(action);

        Assert.Equal("changeRate", exception.ParamName);
    }

    [Fact]
    public void UpdateRates_ReplacesValuesForTheSameCurrencyPairAndDate()
    {
        var rate = CreateRate(
            new DateTime(2026, 8, 7, 9, 30, 0),
            new DateTimeOffset(2026, 8, 7, 7, 0, 0, TimeSpan.Zero),
            cashChangeRate: 46.55073m);

        rate.UpdateRates(
            new DateTime(2026, 8, 7, 10, 45, 0),
            new DateTimeOffset(2026, 8, 7, 8, 0, 0, TimeSpan.Zero),
            changeRate: 47.1m,
            exchangeRateValue: 48.7m,
            cashChangeRate: 46.9m,
            cashExchangeRate: 49.0m,
            centralBankChangeRate: 47.5m,
            centralBankExchangeRate: 47.7m,
            crossRate: 1m);

        Assert.Equal(46.9m, rate.CashChangeRate);
        Assert.Equal(new DateTime(2026, 8, 7, 10, 45, 0), rate.SourceUpdatedAt);
        Assert.Equal(new DateTimeOffset(2026, 8, 7, 8, 0, 0, TimeSpan.Zero), rate.RetrievedAtUtc);
    }

    [Fact]
    public void UpdateRates_RejectsARecordFromAnotherDate()
    {
        var rate = CreateRate(
            new DateTime(2026, 8, 7, 9, 30, 0),
            DateTimeOffset.UtcNow,
            cashChangeRate: 46.55073m);

        var action = () => rate.UpdateRates(
            new DateTime(2026, 8, 8, 9, 30, 0),
            DateTimeOffset.UtcNow,
            changeRate: 47.1m,
            exchangeRateValue: 48.7m,
            cashChangeRate: 46.9m,
            cashExchangeRate: 49.0m,
            centralBankChangeRate: 47.5m,
            centralBankExchangeRate: 47.7m,
            crossRate: 1m);

        Assert.Throws<ArgumentException>(action);
    }

    private static ExchangeRate CreateRate(
        DateTime sourceUpdatedAt,
        DateTimeOffset retrievedAt,
        decimal cashChangeRate)
    {
        return new ExchangeRate(
            baseCurrencyCode: 1,
            foreignCurrencyCode: 56,
            sourceUpdatedAt,
            retrievedAt,
            changeRate: 46.87830m,
            exchangeRateValue: 48.42890m,
            cashChangeRate,
            cashExchangeRate: 48.78997m,
            centralBankChangeRate: 47.48810m,
            centralBankExchangeRate: 47.57360m,
            crossRate: 1m);
    }
}
