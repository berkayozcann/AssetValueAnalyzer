namespace AssetValueAnalyzer.Application.ExchangeRates.Synchronization;

public interface IExchangeRateSynchronizationLock
{
    ValueTask<IAsyncDisposable> AcquireAsync(
        CancellationToken cancellationToken = default);
}
