using AssetValueAnalyzer.Application.Assets.Imports;
using AssetValueAnalyzer.Application.ExchangeRates.External;
using AssetValueAnalyzer.Application.ExchangeRates.Queries;
using AssetValueAnalyzer.Application.ExchangeRates.Synchronization;
using AssetValueAnalyzer.Application.ProducerPriceIndices.Imports;
using AssetValueAnalyzer.Application.Reports.Calculation;
using AssetValueAnalyzer.Application.Reports.Creation;
using AssetValueAnalyzer.Infrastructure.Imports.Assets;
using AssetValueAnalyzer.Infrastructure.Imports.ProducerPriceIndices;
using AssetValueAnalyzer.Infrastructure.Integrations.Finmaks;
using AssetValueAnalyzer.Infrastructure.Persistence;
using AssetValueAnalyzer.Infrastructure.Persistence.ExchangeRates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AssetValueAnalyzer.Infrastructure;

public static class DependencyInjection
{
    public const string DatabaseConnectionName = "AssetValueAnalyzer";

    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(DatabaseConnectionName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{DatabaseConnectionName}' has not been configured.");
        }

        services
            .AddOptions<FinmaksOptions>()
            .Bind(configuration.GetSection(FinmaksOptions.SectionName))
            .Validate(
                options => options.BaseAddress.IsAbsoluteUri,
                "Finmaks:BaseAddress must be an absolute URI.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ApiKey),
                "Finmaks:ApiKey has not been configured.")
            .ValidateOnStart();

        services.AddDbContext<AssetValueAnalyzerDbContext>(options =>
            options.UseSqlServer(connectionString));

        services
            .AddHttpClient<IFinmaksExchangeRateClient, FinmaksExchangeRateClient>(
                (serviceProvider, httpClient) =>
                {
                    var options = serviceProvider
                        .GetRequiredService<IOptions<FinmaksOptions>>()
                        .Value;

                    httpClient.BaseAddress = options.BaseAddress;
                    httpClient.Timeout = TimeSpan.FromSeconds(30);
                })
            .RemoveAllLoggers();

        services.AddScoped<IExchangeRateStore, EfExchangeRateStore>();
        services.AddScoped<ExchangeRateSynchronizationService>();
        services.AddScoped<InitializeExchangeRatesService>();
        services.AddScoped<IAssetFileParser, ClosedXmlAssetFileParser>();
        services.AddScoped<IAssetFileParser, XmlAssetFileParser>();
        services.AddScoped<ReadAssetValuesService>();
        services.AddScoped<IProducerPriceIndexFileParser, ClosedXmlProducerPriceIndexFileParser>();
        services.AddScoped<IProducerPriceIndexFileParser, XmlProducerPriceIndexFileParser>();
        services.AddScoped<ReadProducerPriceIndicesService>();
        services.AddSingleton<FinancialImpactCalculator>();
        services.AddSingleton<FinancialImpactReportRangeValidator>();
        services.AddScoped<IUsdCashChangeRateReader, EfUsdCashChangeRateReader>();
        services.AddScoped<ICurrentUsdExchangeRateReader, EfCurrentUsdExchangeRateReader>();
        services.AddScoped<CreateFinancialImpactReportService>();
        services.AddSingleton<TimeProvider>(TimeProvider.System);

        return services;
    }
}
