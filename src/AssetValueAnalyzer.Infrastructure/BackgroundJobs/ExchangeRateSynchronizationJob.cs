using AssetValueAnalyzer.Application.ExchangeRates.Synchronization;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace AssetValueAnalyzer.Infrastructure.BackgroundJobs;

public sealed class ExchangeRateSynchronizationJob(
    InitializeExchangeRatesService initializationService,
    IExchangeRateSynchronizationNotifier synchronizationNotifier,
    ILogger<ExchangeRateSynchronizationJob> logger)
{
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    [DisableConcurrentExecution(timeoutInSeconds: 120)]
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Recurring exchange-rate synchronization started.");

        var result = await initializationService.InitializeAsync(cancellationToken);

        await synchronizationNotifier.NotifyCompletedAsync(cancellationToken);

        if (result.Synchronization.ReceivedCount == 0)
        {
            logger.LogInformation(
                "Recurring exchange-rate synchronization completed. " +
                "Current-day exchange rates are not available yet; " +
                "the next scheduled run will retry.");
            return;
        }

        logger.LogInformation(
            "Recurring exchange-rate synchronization completed. Received: {ReceivedCount}; " +
            "inserted: {InsertedCount}; updated: {UpdatedCount}; unchanged: {UnchangedCount}.",
            result.Synchronization.ReceivedCount,
            result.Synchronization.InsertedCount,
            result.Synchronization.UpdatedCount,
            result.Synchronization.UnchangedCount);
    }
}
