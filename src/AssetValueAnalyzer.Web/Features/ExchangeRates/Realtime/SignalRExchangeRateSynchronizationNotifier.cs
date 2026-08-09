using AssetValueAnalyzer.Application.ExchangeRates.Synchronization;
using Microsoft.AspNetCore.SignalR;

namespace AssetValueAnalyzer.Web.Features.ExchangeRates.Realtime;

public sealed class SignalRExchangeRateSynchronizationNotifier(
    IHubContext<ExchangeRateHub> hubContext)
    : IExchangeRateSynchronizationNotifier
{
    public const string SynchronizationCompletedEvent = "exchangeRatesSynchronized";

    public Task NotifyCompletedAsync(CancellationToken cancellationToken = default) =>
        hubContext.Clients.All.SendAsync(
            SynchronizationCompletedEvent,
            cancellationToken);
}
