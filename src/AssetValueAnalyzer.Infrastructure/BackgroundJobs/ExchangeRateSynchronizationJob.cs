using AssetValueAnalyzer.Application.ExchangeRates.Synchronization;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace AssetValueAnalyzer.Infrastructure.BackgroundJobs;

public sealed class ExchangeRateSynchronizationJob(
    ExchangeRateSynchronizationService synchronizationService,
    ILogger<ExchangeRateSynchronizationJob> logger)
{
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    [DisableConcurrentExecution(timeoutInSeconds: 120)]
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Recurring exchange-rate synchronization started.");

        var result = await synchronizationService.SynchronizeAsync(
            new SyncExchangeRatesRequest(),
            cancellationToken);

        logger.LogInformation(
            "Recurring exchange-rate synchronization completed. Received: {ReceivedCount}; " +
            "inserted: {InsertedCount}; updated: {UpdatedCount}; unchanged: {UnchangedCount}.",
            result.ReceivedCount,
            result.InsertedCount,
            result.UpdatedCount,
            result.UnchangedCount);
    }
}
