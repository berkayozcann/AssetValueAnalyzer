using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AssetValueAnalyzer.IntegrationTests.Support;

internal static class HealthCheckTestServices
{
    public static void ReplaceReadinessChecksWithHealthyProbe(
        this IServiceCollection services)
        => services.ReplaceReadinessChecksWithProbe(HealthStatus.Healthy);

    public static void ReplaceReadinessChecksWithUnhealthyProbe(
        this IServiceCollection services)
        => services.ReplaceReadinessChecksWithProbe(HealthStatus.Unhealthy);

    private static void ReplaceReadinessChecksWithProbe(
        this IServiceCollection services,
        HealthStatus status)
    {
        services.PostConfigure<HealthCheckServiceOptions>(options =>
        {
            options.Registrations.Clear();
            options.Registrations.Add(
                new HealthCheckRegistration(
                    "test-readiness",
                    new FixedStatusTestHealthCheck(status),
                    HealthStatus.Unhealthy,
                    ["ready"]));
        });
    }

    private sealed class FixedStatusTestHealthCheck(
        HealthStatus status) : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                status == HealthStatus.Healthy
                    ? HealthCheckResult.Healthy()
                    : HealthCheckResult.Unhealthy());
    }
}
