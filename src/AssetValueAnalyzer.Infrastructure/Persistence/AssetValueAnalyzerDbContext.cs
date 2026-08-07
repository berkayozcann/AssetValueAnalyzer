using AssetValueAnalyzer.Domain.ExchangeRates;
using Microsoft.EntityFrameworkCore;

namespace AssetValueAnalyzer.Infrastructure.Persistence;

public sealed class AssetValueAnalyzerDbContext : DbContext
{
    public AssetValueAnalyzerDbContext(
        DbContextOptions<AssetValueAnalyzerDbContext> options)
        : base(options)
    {
    }

    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AssetValueAnalyzerDbContext).Assembly);
    }
}
