using AssetValueAnalyzer.Application.ExchangeRates.Synchronization;

namespace AssetValueAnalyzer.Infrastructure.BackgroundJobs;

internal sealed class NullExchangeRateSynchronizationNotifier
    : IExchangeRateSynchronizationNotifier
{
    public Task NotifyCompletedAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
