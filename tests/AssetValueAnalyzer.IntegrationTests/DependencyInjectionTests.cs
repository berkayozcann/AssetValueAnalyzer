using AssetValueAnalyzer.Application.Assets.Imports;
using AssetValueAnalyzer.Application.ExchangeRates.External;
using AssetValueAnalyzer.Application.ExchangeRates.Queries;
using AssetValueAnalyzer.Application.ExchangeRates.Synchronization;
using AssetValueAnalyzer.Application.ProducerPriceIndices.Imports;
using AssetValueAnalyzer.Application.Reports.Calculation;
using AssetValueAnalyzer.Application.Reports.Creation;
using AssetValueAnalyzer.Application.Reports.Exporting;
using AssetValueAnalyzer.Infrastructure;
using AssetValueAnalyzer.Infrastructure.BackgroundJobs;
using AssetValueAnalyzer.Infrastructure.Imports.Assets;
using AssetValueAnalyzer.Infrastructure.Imports.ProducerPriceIndices;
using AssetValueAnalyzer.Infrastructure.Integrations.Finmaks;
using AssetValueAnalyzer.Infrastructure.Persistence;
using AssetValueAnalyzer.Infrastructure.Persistence.ExchangeRates;
using AssetValueAnalyzer.Infrastructure.Reports.Exporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AssetValueAnalyzer.IntegrationTests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddExchangeRateReadServices_WithoutFinmaksSettings_RegistersOnlyReadDependencies()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{DependencyInjection.DatabaseConnectionName}"] =
                    "Server=example.invalid;Database=AssetValueAnalyzer;Integrated Security=True;TrustServerCertificate=True"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddExchangeRateReadServices(configuration);

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
        using var scope = provider.CreateScope();

        Assert.IsType<EfExchangeRateReader>(
            scope.ServiceProvider.GetRequiredService<IExchangeRateReader>());
        Assert.True(scope.ServiceProvider
            .GetRequiredService<AssetValueAnalyzerDbContext>()
            .Database
            .IsSqlServer());
        Assert.Null(scope.ServiceProvider.GetService<IFinmaksExchangeRateClient>());
        Assert.Null(scope.ServiceProvider.GetService<ExchangeRateSynchronizationService>());
    }

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
            scope.ServiceProvider.GetRequiredService<
                IExchangeRateSynchronizationNotifier>());
        Assert.NotNull(
            scope.ServiceProvider.GetRequiredService<InitializeExchangeRatesService>());
        var assetParsers = scope.ServiceProvider
            .GetServices<IAssetFileParser>()
            .ToArray();
        Assert.Collection(
            assetParsers,
            parser => Assert.IsType<XlsxAssetFileParser>(parser));
        Assert.NotNull(
            scope.ServiceProvider.GetRequiredService<ReadAssetValuesService>());
        var producerPriceIndexParsers = scope.ServiceProvider
            .GetServices<IProducerPriceIndexFileParser>()
            .ToArray();
        Assert.Collection(
            producerPriceIndexParsers,
            parser => Assert.IsType<XlsxProducerPriceIndexFileParser>(parser));
        Assert.NotNull(
            scope.ServiceProvider.GetRequiredService<ReadProducerPriceIndicesService>());
        Assert.NotNull(
            scope.ServiceProvider.GetRequiredService<FinancialImpactCalculator>());
        Assert.NotNull(
            scope.ServiceProvider.GetRequiredService<FinancialImpactReportRangeValidator>());
        Assert.IsType<EfUsdCashChangeRateReader>(
            scope.ServiceProvider.GetRequiredService<IUsdCashChangeRateReader>());
        Assert.IsType<EfCurrentUsdExchangeRateReader>(
            scope.ServiceProvider.GetRequiredService<ICurrentUsdExchangeRateReader>());
        Assert.IsType<EfExchangeRateReader>(
            scope.ServiceProvider.GetRequiredService<IExchangeRateReader>());
        Assert.NotNull(
            scope.ServiceProvider.GetRequiredService<CreateFinancialImpactReportService>());
        Assert.IsType<XlsxFinancialImpactReportExporter>(
            scope.ServiceProvider.GetRequiredService<IFinancialImpactReportExporter>());
        Assert.Same(
            TimeProvider.System,
            scope.ServiceProvider.GetRequiredService<TimeProvider>());
        var recurringJobOptions = scope.ServiceProvider
            .GetRequiredService<IOptions<ExchangeRateRecurringJobOptions>>()
            .Value;
        Assert.Equal(
            ExchangeRateRecurringJobOptions.DefaultIntervalMinutes,
            recurringJobOptions.IntervalMinutes);
        Assert.Equal("*/3 * * * *", Hangfire.Cron.MinuteInterval(
            recurringJobOptions.IntervalMinutes));

        var dbContext = scope.ServiceProvider
            .GetRequiredService<AssetValueAnalyzerDbContext>();

        Assert.True(dbContext.Database.IsSqlServer());
    }

    [Fact]
    public void AddInfrastructureServices_WithRecurringJobEnabled_RegistersHangfireServices()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            [$"{ExchangeRateRecurringJobOptions.SectionName}:Enabled"] = "true",
            [$"{ExchangeRateRecurringJobOptions.SectionName}:IntervalMinutes"] = "3"
        });
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddInfrastructureServices(configuration);

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(Hangfire.IRecurringJobManager));
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(ExchangeRateSynchronizationJob));
        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType ==
                typeof(ExchangeRateRecurringJobRegistrationHostedService));
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

    [Fact]
    public void AddInfrastructureServices_WithInvalidRecurringInterval_RejectsOptions()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            [$"{ExchangeRateRecurringJobOptions.SectionName}:IntervalMinutes"] = "7"
        });
        var services = new ServiceCollection();

        services.AddInfrastructureServices(configuration);

        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
            provider
                .GetRequiredService<IOptions<ExchangeRateRecurringJobOptions>>()
                .Value);
    }

    private static IConfiguration CreateConfiguration(
        IReadOnlyDictionary<string, string?>? overrides = null)
    {
        var values = new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{DependencyInjection.DatabaseConnectionName}"] =
                "Server=example.invalid;Database=AssetValueAnalyzer;Integrated Security=True;TrustServerCertificate=True",
            [$"{FinmaksOptions.SectionName}:BaseAddress"] = "https://example.test/",
            [$"{FinmaksOptions.SectionName}:ApiKey"] = "integration-test-key"
        };

        if (overrides is not null)
        {
            foreach (var pair in overrides)
            {
                values[pair.Key] = pair.Value;
            }
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
