namespace AssetValueAnalyzer.Infrastructure.Integrations.Finmaks;

public sealed class FinmaksApiException(string message) : Exception(message);
