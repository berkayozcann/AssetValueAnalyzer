using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AssetValueAnalyzer.Infrastructure.Persistence;

public sealed class EfDatabaseStartupService(
    AssetValueAnalyzerDbContext dbContext,
    ILogger<EfDatabaseStartupService> logger) : IDatabaseStartupService
{
    public async Task ApplyMigrationsAsync(
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Applying pending AssetValueAnalyzer database migrations.");

        try
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Automatic AssetValueAnalyzer database migration failed. " +
                "Verify the MSSQL connection string, credentials and database permissions.",
                exception);
        }

        logger.LogInformation("AssetValueAnalyzer database migrations are current.");
    }

    public async Task EnsureReadyAsync(
        CancellationToken cancellationToken = default)
    {
        bool canConnect;

        try
        {
            canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw CreateConnectionException(exception);
        }

        if (!canConnect)
        {
            throw CreateConnectionException();
        }

        string[] pendingMigrations;

        try
        {
            pendingMigrations = (await dbContext.Database
                    .GetPendingMigrationsAsync(cancellationToken))
                .ToArray();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "The AssetValueAnalyzer database schema could not be inspected. " +
                "Start the Web application or apply EF Core migrations first.",
                exception);
        }

        if (pendingMigrations.Length > 0)
        {
            throw new InvalidOperationException(
                "The AssetValueAnalyzer database schema is not ready. " +
                "Start the Web application or apply EF Core migrations first. " +
                $"Pending migrations: {string.Join(", ", pendingMigrations)}.");
        }

        logger.LogInformation("AssetValueAnalyzer database connection and schema are ready.");
    }

    private static InvalidOperationException CreateConnectionException(
        Exception? innerException = null) =>
        new(
            "The AssetValueAnalyzer database is unavailable. " +
            "Verify the MSSQL connection string, credentials and server availability.",
            innerException);
}
