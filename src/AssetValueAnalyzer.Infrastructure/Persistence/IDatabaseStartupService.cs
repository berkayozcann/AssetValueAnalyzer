namespace AssetValueAnalyzer.Infrastructure.Persistence;

public interface IDatabaseStartupService
{
    Task ApplyMigrationsAsync(CancellationToken cancellationToken = default);

    Task EnsureReadyAsync(CancellationToken cancellationToken = default);
}
