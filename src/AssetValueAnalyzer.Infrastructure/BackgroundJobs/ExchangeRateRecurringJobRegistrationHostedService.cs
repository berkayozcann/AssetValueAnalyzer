using Hangfire;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AssetValueAnalyzer.Infrastructure.BackgroundJobs;

public sealed class ExchangeRateRecurringJobRegistrationHostedService(
    IRecurringJobManager recurringJobManager,
    IOptions<ExchangeRateRecurringJobOptions> options,
    ILogger<ExchangeRateRecurringJobRegistrationHostedService> logger)
    : IHostedService
{
    public const string RecurringJobId = "exchange-rates-current-day-sync";

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var cronExpression = Cron.MinuteInterval(options.Value.IntervalMinutes);

            recurringJobManager.AddOrUpdate<ExchangeRateSynchronizationJob>(
                RecurringJobId,
                job => job.ExecuteAsync(CancellationToken.None),
                cronExpression,
                new RecurringJobOptions
                {
                    TimeZone = TimeZoneInfo.Utc
                });

            logger.LogInformation(
                "Recurring exchange-rate job registered with a {IntervalMinutes}-minute interval.",
                options.Value.IntervalMinutes);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Recurring exchange-rate job registration failed. The web host will continue running.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
