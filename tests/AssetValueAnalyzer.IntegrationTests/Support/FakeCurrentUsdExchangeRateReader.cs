using AssetValueAnalyzer.Application.ExchangeRates.Queries;

namespace AssetValueAnalyzer.IntegrationTests.Support;

internal sealed class FakeCurrentUsdExchangeRateReader(
    CurrentUsdExchangeRate? rate = null) : ICurrentUsdExchangeRateReader
{
    public Task<CurrentUsdExchangeRate?> ReadAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(rate);
}
