using AssetValueAnalyzer.Application.ExchangeRates.Synchronization;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AssetValueAnalyzer.Web.HealthChecks;

public sealed class ExchangeRateBackfillReadinessHealthCheck(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var exchangeRateStore = scope.ServiceProvider
                .GetRequiredService<IExchangeRateStore>();
            var state = await exchangeRateStore.GetBackfillStateAsync(
                cancellationToken);
            var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);

            return state?.CompletedThroughDate == today
                ? HealthCheckResult.Healthy(
                    "Historical exchange-rate backfill is current.")
                : HealthCheckResult.Unhealthy(
                    "Historical exchange-rate backfill is not current.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "Historical exchange-rate backfill readiness check failed.",
                exception);
        }
    }
}
