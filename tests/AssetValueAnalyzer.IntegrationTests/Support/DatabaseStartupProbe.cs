using AssetValueAnalyzer.Infrastructure.Persistence;

namespace AssetValueAnalyzer.IntegrationTests.Support;

public sealed class DatabaseStartupProbe(
    Exception? migrationException = null,
    Exception? readinessException = null) : IDatabaseStartupService
{
    private int _applyMigrationsCallCount;
    private int _ensureReadyCallCount;

    public int ApplyMigrationsCallCount => _applyMigrationsCallCount;

    public int EnsureReadyCallCount => _ensureReadyCallCount;

    public Task ApplyMigrationsAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _applyMigrationsCallCount);

        return migrationException is null
            ? Task.CompletedTask
            : Task.FromException(migrationException);
    }

    public Task EnsureReadyAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _ensureReadyCallCount);

        return readinessException is null
            ? Task.CompletedTask
            : Task.FromException(readinessException);
    }
}
