using AssetValueAnalyzer.Application.ExchangeRates.Synchronization;
using AssetValueAnalyzer.Domain.ExchangeRates;
using AssetValueAnalyzer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ApiDatabaseReadinessHealthCheck = AssetValueAnalyzer.Api.HealthChecks.DatabaseReadinessHealthCheck;
using WebDatabaseReadinessHealthCheck = AssetValueAnalyzer.Web.HealthChecks.DatabaseReadinessHealthCheck;
using ExchangeRateBackfillReadinessHealthCheck = AssetValueAnalyzer.Web.HealthChecks.ExchangeRateBackfillReadinessHealthCheck;

namespace AssetValueAnalyzer.IntegrationTests.Persistence;

public sealed class ReadinessHealthCheckTests
{
    [Fact]
    public async Task DatabaseReadiness_WhenDatabaseIsReachable_ReturnsHealthy()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AssetValueAnalyzerDbContext>(options =>
            options.UseSqlite("Data Source=:memory:"));
        await using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var webHealthCheck = new WebDatabaseReadinessHealthCheck(scopeFactory);
        var apiHealthCheck = new ApiDatabaseReadinessHealthCheck(scopeFactory);

        var webResult = await webHealthCheck.CheckHealthAsync(new HealthCheckContext());
        var apiResult = await apiHealthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, webResult.Status);
        Assert.Equal(HealthStatus.Healthy, apiResult.Status);
    }

    [Fact]
    public async Task DatabaseReadiness_WhenDatabaseIsUnavailable_ReturnsUnhealthy()
    {
        var unavailablePath = Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString("N"),
            "missing.db");
        var services = new ServiceCollection();
        services.AddDbContext<AssetValueAnalyzerDbContext>(options =>
            options.UseSqlite($"Data Source={unavailablePath};Mode=ReadOnly"));
        await using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var webHealthCheck = new WebDatabaseReadinessHealthCheck(scopeFactory);
        var apiHealthCheck = new ApiDatabaseReadinessHealthCheck(scopeFactory);

        var webResult = await webHealthCheck.CheckHealthAsync(new HealthCheckContext());
        var apiResult = await apiHealthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, webResult.Status);
        Assert.Equal(HealthStatus.Unhealthy, apiResult.Status);
    }

    [Theory]
    [InlineData(-1, HealthStatus.Unhealthy)]
    [InlineData(0, HealthStatus.Healthy)]
    [InlineData(1, HealthStatus.Unhealthy)]
    public async Task BackfillReadiness_RequiresCheckpointForToday(
        int checkpointDayOffset,
        HealthStatus expectedStatus)
    {
        var today = new DateOnly(2026, 8, 11);
        var timeProvider = new FixedTimeProvider(
            new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero));
        var services = new ServiceCollection();
        services.AddSingleton<IExchangeRateStore>(
            new StubExchangeRateStore(
                new ExchangeRateBackfillState(
                    today.AddDays(checkpointDayOffset),
                    timeProvider.GetUtcNow())));
        await using var provider = services.BuildServiceProvider();
        var healthCheck = new ExchangeRateBackfillReadinessHealthCheck(
            provider.GetRequiredService<IServiceScopeFactory>(),
            timeProvider);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(expectedStatus, result.Status);
    }

    [Fact]
    public async Task BackfillReadiness_WithoutCheckpoint_ReturnsUnhealthy()
    {
        var timeProvider = new FixedTimeProvider(
            new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero));
        var services = new ServiceCollection();
        services.AddSingleton<IExchangeRateStore>(
            new StubExchangeRateStore(state: null));
        await using var provider = services.BuildServiceProvider();
        var healthCheck = new ExchangeRateBackfillReadinessHealthCheck(
            provider.GetRequiredService<IServiceScopeFactory>(),
            timeProvider);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    private sealed class StubExchangeRateStore(
        ExchangeRateBackfillState? state) : IExchangeRateStore
    {
        public Task<ExchangeRateBackfillState?> GetBackfillStateAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(state);

        public Task MarkBackfillCompletedAsync(
            DateOnly completedThroughDate,
            DateTimeOffset completedAtUtc,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ExchangeRateUpsertResult> UpsertAsync(
            IReadOnlyCollection<ExchangeRate> exchangeRates,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider(
        DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
