namespace AssetValueAnalyzer.Infrastructure.Integrations.Finmaks;

public sealed class FinmaksOptions
{
    public const string SectionName = "Finmaks";

    public Uri BaseAddress { get; init; } = new("https://testapi.finmaks.com/");

    public string ApiKey { get; init; } = string.Empty;
}
