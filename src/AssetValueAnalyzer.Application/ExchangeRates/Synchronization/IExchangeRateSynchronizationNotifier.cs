namespace AssetValueAnalyzer.Application.ExchangeRates.Synchronization;

public interface IExchangeRateSynchronizationNotifier
{
    Task NotifyCompletedAsync(CancellationToken cancellationToken = default);
}
