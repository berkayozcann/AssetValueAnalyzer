using AssetValueAnalyzer.IntegrationTests.Support;

namespace AssetValueAnalyzer.IntegrationTests.Http;

public sealed class DatabaseStartupHostTests
{
    [Fact]
    public void WebHost_WhenMigrationFails_DoesNotStart()
    {
        const string message = "Controlled migration failure.";
        var probe = new DatabaseStartupProbe(
            migrationException: new InvalidOperationException(message));
        using var factory = new AssetValueAnalyzerWebApplicationFactory(probe);

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains(message, exception.ToString());
        Assert.Equal(1, probe.ApplyMigrationsCallCount);
        Assert.Equal(0, probe.EnsureReadyCallCount);
    }

    [Fact]
    public void ApiHost_WhenDatabaseIsNotReady_DoesNotStart()
    {
        const string message = "Controlled readiness failure.";
        var probe = new DatabaseStartupProbe(
            readinessException: new InvalidOperationException(message));
        using var factory = new AssetValueAnalyzerApiApplicationFactory(probe);

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains(message, exception.ToString());
        Assert.Equal(0, probe.ApplyMigrationsCallCount);
        Assert.Equal(1, probe.EnsureReadyCallCount);
    }
}
