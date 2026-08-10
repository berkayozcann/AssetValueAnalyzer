using AssetValueAnalyzer.Infrastructure.Persistence.ExchangeRates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetValueAnalyzer.Infrastructure.Persistence.Configurations;

public sealed class ExchangeRateBackfillCheckpointConfiguration
    : IEntityTypeConfiguration<ExchangeRateBackfillCheckpoint>
{
    public void Configure(EntityTypeBuilder<ExchangeRateBackfillCheckpoint> builder)
    {
        builder.ToTable(
            "ExchangeRateBackfillCheckpoints",
            table => table.HasCheckConstraint(
                "CK_ExchangeRateBackfillCheckpoints_SingletonId",
                "[Id] = 1"));

        builder.HasKey(checkpoint => checkpoint.Id);

        builder.Property(checkpoint => checkpoint.Id)
            .ValueGeneratedNever();

        builder.Property(checkpoint => checkpoint.CompletedThroughDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(checkpoint => checkpoint.CompletedAtUtc)
            .HasPrecision(0)
            .IsRequired();
    }
}
