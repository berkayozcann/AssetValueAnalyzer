using AssetValueAnalyzer.Domain.ExchangeRates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetValueAnalyzer.Infrastructure.Persistence.Configurations;

public sealed class ExchangeRateConfiguration : IEntityTypeConfiguration<ExchangeRate>
{
    public void Configure(EntityTypeBuilder<ExchangeRate> builder)
    {
        builder.ToTable("ExchangeRates");

        builder.HasKey(rate => rate.Id);

        builder.Property(rate => rate.Id)
            .ValueGeneratedOnAdd();

        builder.Property(rate => rate.BaseCurrencyCode)
            .IsRequired();

        builder.Property(rate => rate.ForeignCurrencyCode)
            .IsRequired();

        builder.Property(rate => rate.RateDate)
            .HasColumnType("date")
            .IsRequired();

        ConfigureRateValue(builder.Property(rate => rate.ChangeRate));
        ConfigureRateValue(
            builder.Property(rate => rate.ExchangeRateValue)
                .HasColumnName("ExchangeRate"));
        ConfigureRateValue(builder.Property(rate => rate.CashChangeRate));
        ConfigureRateValue(builder.Property(rate => rate.CashExchangeRate));
        ConfigureRateValue(builder.Property(rate => rate.CentralBankChangeRate));
        ConfigureRateValue(builder.Property(rate => rate.CentralBankExchangeRate));
        ConfigureRateValue(builder.Property(rate => rate.CrossRate));

        builder.Property(rate => rate.SourceUpdatedAt)
            .HasPrecision(0)
            .IsRequired();

        builder.Property(rate => rate.RetrievedAtUtc)
            .HasPrecision(0)
            .IsRequired();

        builder.HasIndex(rate => new
        {
            rate.BaseCurrencyCode,
            rate.ForeignCurrencyCode,
            rate.RateDate
        })
            .IsUnique()
            .HasDatabaseName("UX_ExchangeRates_CurrencyPair_RateDate");
    }

    private static void ConfigureRateValue(PropertyBuilder<decimal> propertyBuilder)
    {
        propertyBuilder
            .HasPrecision(19, 8)
            .IsRequired();
    }
}
