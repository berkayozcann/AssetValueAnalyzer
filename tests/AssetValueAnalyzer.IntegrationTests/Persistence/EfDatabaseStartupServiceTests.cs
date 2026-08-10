using AssetValueAnalyzer.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AssetValueAnalyzer.IntegrationTests.Persistence;

public sealed class EfDatabaseStartupServiceTests
{
    [Fact]
    public async Task EnsureReadyAsync_WithCurrentSchema_Completes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                CREATE TABLE "__EFMigrationsHistory" (
                    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                    "ProductVersion" TEXT NOT NULL
                );
                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                VALUES
                    ('20260807140300_InitialExchangeRates', '10.0.10'),
                    ('20260810195548_AddExchangeRateBackfillCheckpoint', '10.0.10');
                """;
            await command.ExecuteNonQueryAsync();
        }

        await using var dbContext = CreateDbContext(connection);
        var service = CreateService(dbContext);

        await service.EnsureReadyAsync(CancellationToken.None);
    }

    [Fact]
    public async Task EnsureReadyAsync_WithPendingMigrations_ThrowsClearInstruction()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        var service = CreateService(dbContext);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.EnsureReadyAsync(CancellationToken.None));

        Assert.Contains("schema is not ready", exception.Message);
        Assert.Contains("Start the Web application", exception.Message);
        Assert.Contains("20260807140300_InitialExchangeRates", exception.Message);
        Assert.Contains(
            "20260810195548_AddExchangeRateBackfillCheckpoint",
            exception.Message);
    }

    [Fact]
    public async Task EnsureReadyAsync_WhenDatabaseIsUnavailable_ThrowsClearError()
    {
        var unavailablePath = Path.Combine(
            Path.GetTempPath(),
            $"asset-value-analyzer-missing-{Guid.NewGuid():N}",
            "database.sqlite");
        var options = new DbContextOptionsBuilder<AssetValueAnalyzerDbContext>()
            .UseSqlite($"Data Source={unavailablePath};Mode=ReadOnly")
            .Options;
        await using var dbContext = new AssetValueAnalyzerDbContext(options);
        var service = CreateService(dbContext);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.EnsureReadyAsync(CancellationToken.None));

        Assert.Contains("database is unavailable", exception.Message);
        Assert.Contains("connection string", exception.Message);
    }

    private static AssetValueAnalyzerDbContext CreateDbContext(
        SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AssetValueAnalyzerDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AssetValueAnalyzerDbContext(options);
    }

    private static EfDatabaseStartupService CreateService(
        AssetValueAnalyzerDbContext dbContext) =>
        new(dbContext, NullLogger<EfDatabaseStartupService>.Instance);
}
