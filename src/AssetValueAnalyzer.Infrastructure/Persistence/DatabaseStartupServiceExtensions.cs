using Microsoft.Extensions.DependencyInjection;

namespace AssetValueAnalyzer.Infrastructure.Persistence;

public static class DatabaseStartupServiceExtensions
{
    public static async Task ApplyDatabaseMigrationsAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var startupService = scope.ServiceProvider
            .GetRequiredService<IDatabaseStartupService>();

        await startupService.ApplyMigrationsAsync(cancellationToken);
    }

    public static async Task EnsureDatabaseReadyAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var startupService = scope.ServiceProvider
            .GetRequiredService<IDatabaseStartupService>();

        await startupService.EnsureReadyAsync(cancellationToken);
    }
}
