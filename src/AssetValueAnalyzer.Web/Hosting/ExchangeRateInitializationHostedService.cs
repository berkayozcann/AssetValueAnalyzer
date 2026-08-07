using AssetValueAnalyzer.Application.ExchangeRates.Synchronization;

namespace AssetValueAnalyzer.Web.Hosting;

public sealed class ExchangeRateInitializationHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<ExchangeRateInitializationHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation("Exchange-rate initialization started.");

            await using var scope = scopeFactory.CreateAsyncScope();
            var initializationService = scope.ServiceProvider
                .GetRequiredService<InitializeExchangeRatesService>();

            var result = await initializationService.InitializeAsync(stoppingToken);

            logger.LogInformation(
                "Exchange-rate initialization completed. Previous latest date: {PreviousLatestDate}; " +
                "requested range: {StartDate} - {EndDate}; received: {ReceivedCount}; " +
                "inserted: {InsertedCount}; updated: {UpdatedCount}.",
                result.PreviouslyLatestRateDate,
                result.RequestedStartDate,
                result.RequestedEndDate,
                result.Synchronization.ReceivedCount,
                result.Synchronization.InsertedCount,
                result.Synchronization.UpdatedCount);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Exchange-rate initialization was cancelled during shutdown.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Exchange-rate initialization failed.");
        }
    }
}
