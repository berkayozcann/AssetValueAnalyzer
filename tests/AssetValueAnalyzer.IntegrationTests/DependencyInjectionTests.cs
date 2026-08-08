using AssetValueAnalyzer.Application.Assets.Imports;
using AssetValueAnalyzer.Application.ExchangeRates.External;
using AssetValueAnalyzer.Application.ExchangeRates.Synchronization;
using AssetValueAnalyzer.Application.ProducerPriceIndices.Imports;
using AssetValueAnalyzer.Infrastructure;
using AssetValueAnalyzer.Infrastructure.Imports.Assets;
using AssetValueAnalyzer.Infrastructure.Imports.ProducerPriceIndices;
using AssetValueAnalyzer.Infrastructure.Integrations.Finmaks;
using AssetValueAnalyzer.Infrastructure.Persistence;
using AssetValueAnalyzer.Infrastructure.Persistence.ExchangeRates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AssetValueAnalyzer.IntegrationTests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddInfrastructureServices_RegistersExchangeRateDependencies()
    {
        var configuration = CreateConfiguration();
        var services = new ServiceCollection();

        services.AddInfrastructureServices(configuration);

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
        using var scope = provider.CreateScope();

        Assert.IsType<FinmaksExchangeRateClient>(
            scope.ServiceProvider.GetRequiredService<IFinmaksExchangeRateClient>());
        Assert.IsType<EfExchangeRateStore>(
            scope.ServiceProvider.GetRequiredService<IExchangeRateStore>());
        Assert.NotNull(
            scope.ServiceProvider.GetRequiredService<ExchangeRateSynchronizationService>());
        Assert.NotNull(
            scope.ServiceProvider.GetRequiredService<InitializeExchangeRatesService>());
        var assetParsers = scope.ServiceProvider
            .GetServices<IAssetFileParser>()
            .ToArray();
        Assert.Contains(assetParsers, parser => parser is ClosedXmlAssetFileParser);
        Assert.Contains(assetParsers, parser => parser is XmlAssetFileParser);
        Assert.NotNull(
            scope.ServiceProvider.GetRequiredService<ReadAssetValuesService>());
        Assert.IsType<ClosedXmlProducerPriceIndexFileParser>(
            scope.ServiceProvider.GetRequiredService<IProducerPriceIndexFileParser>());
        Assert.NotNull(
            scope.ServiceProvider.GetRequiredService<ReadProducerPriceIndicesService>());
        Assert.Same(
            TimeProvider.System,
            scope.ServiceProvider.GetRequiredService<TimeProvider>());

        var dbContext = scope.ServiceProvider
            .GetRequiredService<AssetValueAnalyzerDbContext>();

        Assert.True(dbContext.Database.IsSqlServer());
    }

    [Fact]
    public void AddInfrastructureServices_WithoutConnectionString_ThrowsClearError()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructureServices(configuration));

        Assert.Contains(DependencyInjection.DatabaseConnectionName, exception.Message);
    }

    private static IConfiguration CreateConfiguration()
    {
        var values = new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{DependencyInjection.DatabaseConnectionName}"] =
                "Server=example.invalid;Database=AssetValueAnalyzer;Integrated Security=True;TrustServerCertificate=True",
            [$"{FinmaksOptions.SectionName}:BaseAddress"] = "https://example.test/",
            [$"{FinmaksOptions.SectionName}:ApiKey"] = "integration-test-key"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
