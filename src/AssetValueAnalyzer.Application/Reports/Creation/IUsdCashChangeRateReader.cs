namespace AssetValueAnalyzer.Application.Reports.Creation;

public interface IUsdCashChangeRateReader
{
    Task<IReadOnlyList<UsdCashChangeRate>> ReadAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default);
}

public sealed record UsdCashChangeRate(
    DateOnly RateDate,
    decimal Value);
