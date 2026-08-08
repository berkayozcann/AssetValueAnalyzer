namespace AssetValueAnalyzer.Application.ExchangeRates.Queries;

public interface ICurrentUsdExchangeRateReader
{
    Task<CurrentUsdExchangeRate?> ReadAsync(
        CancellationToken cancellationToken = default);
}

public sealed record CurrentUsdExchangeRate(
    decimal Value,
    DateOnly RateDate,
    DateTimeOffset RetrievedAtUtc,
    decimal? PreviousValue);
