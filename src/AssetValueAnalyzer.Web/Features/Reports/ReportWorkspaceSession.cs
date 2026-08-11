using System.Text.Json;
using AssetValueAnalyzer.Application.Assets.Imports;
using AssetValueAnalyzer.Application.ProducerPriceIndices.Imports;

namespace AssetValueAnalyzer.Web.Features.Reports;

public interface IReportWorkspaceSession
{
    ReportWorkspaceSnapshot Get();

    void SaveAssetValues(
        string fileName,
        IReadOnlyList<MonthlyAssetValueInput> values);

    void SaveProducerPriceIndices(
        string fileName,
        IReadOnlyList<MonthlyProducerPriceIndexInput> values);

    void ClearAssetValues();

    void ClearProducerPriceIndices();

    void SaveCompletedReport(ReportPageViewModel report);

    void SaveWizardState(ReportWizardStateSnapshot wizardState);

    void Clear();
}

public sealed record ReportWorkspaceSnapshot(
    ReportDataFileSnapshot? AssetValues,
    ReportDataFileSnapshot? ProducerPriceIndices,
    ReportPageViewModel? CompletedReport)
{
    public static ReportWorkspaceSnapshot Empty { get; } = new(null, null, null);

    public ReportWizardStateSnapshot WizardState { get; init; } = ReportWizardStateSnapshot.Empty;

    public ReportWorkspaceStatus Status => CompletedReport is not null
        ? ReportWorkspaceStatus.Completed
        : AssetValues is not null || ProducerPriceIndices is not null
            ? ReportWorkspaceStatus.Draft
            : ReportWorkspaceStatus.Empty;
}

public sealed record ReportWizardStateSnapshot(
    int Step,
    DateOnly? StartMonth,
    DateOnly? EndMonth)
{
    public static ReportWizardStateSnapshot Empty { get; } = new(1, null, null);
}

public sealed record ReportDataFileSnapshot(
    string FileName,
    IReadOnlyList<ReportMonthlyValueSnapshot> Values)
{
    public int ParsedCount => Values.Count;

    public DateOnly? FirstMonth => Values.Count == 0
        ? null
        : Values.Min(value => value.Month);

    public DateOnly? LastMonth => Values.Count == 0
        ? null
        : Values.Max(value => value.Month);
}

public sealed record ReportMonthlyValueSnapshot(
    DateOnly Month,
    decimal Value);

public enum ReportWorkspaceStatus
{
    Empty,
    Draft,
    Completed
}

public sealed class ReportWorkspaceSession(
    IHttpContextAccessor httpContextAccessor) : IReportWorkspaceSession
{
    private const string SessionKey = "AssetValueAnalyzer.ReportWorkspace";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private ISession Session => httpContextAccessor.HttpContext?.Session
        ?? throw new InvalidOperationException("Aktif HTTP oturumu bulunamadı.");

    public ReportWorkspaceSnapshot Get()
    {
        var serialized = Session.GetString(SessionKey);

        return string.IsNullOrWhiteSpace(serialized)
            ? ReportWorkspaceSnapshot.Empty
            : JsonSerializer.Deserialize<ReportWorkspaceSnapshot>(serialized, SerializerOptions)
                ?? ReportWorkspaceSnapshot.Empty;
    }

    public void SaveAssetValues(
        string fileName,
        IReadOnlyList<MonthlyAssetValueInput> values)
    {
        var current = Get();
        var file = new ReportDataFileSnapshot(
            Path.GetFileName(fileName),
            values
                .Select(value => new ReportMonthlyValueSnapshot(value.Month, value.Amount))
                .ToArray());

        Save(current with
        {
            AssetValues = file,
            CompletedReport = null,
            WizardState = ReportWizardStateSnapshot.Empty
        });
    }

    public void SaveProducerPriceIndices(
        string fileName,
        IReadOnlyList<MonthlyProducerPriceIndexInput> values)
    {
        var current = Get();
        var file = new ReportDataFileSnapshot(
            Path.GetFileName(fileName),
            values
                .Select(value => new ReportMonthlyValueSnapshot(value.Month, value.Value))
                .ToArray());

        Save(current with
        {
            ProducerPriceIndices = file,
            CompletedReport = null,
            WizardState = ReportWizardStateSnapshot.Empty
        });
    }

    public void ClearAssetValues()
    {
        var current = Get();
        Save(current with
        {
            AssetValues = null,
            CompletedReport = null,
            WizardState = ReportWizardStateSnapshot.Empty
        });
    }

    public void ClearProducerPriceIndices()
    {
        var current = Get();
        Save(current with
        {
            ProducerPriceIndices = null,
            CompletedReport = null,
            WizardState = ReportWizardStateSnapshot.Empty
        });
    }

    public void SaveCompletedReport(ReportPageViewModel report)
    {
        var current = Get();
        Save(current with
        {
            CompletedReport = report,
            WizardState = ReportWizardStateSnapshot.Empty
        });
    }

    public void SaveWizardState(ReportWizardStateSnapshot wizardState)
    {
        var current = Get();
        Save(current with { WizardState = wizardState });
    }

    public void Clear() => Session.Remove(SessionKey);

    private void Save(ReportWorkspaceSnapshot snapshot)
    {
        if (snapshot.Status == ReportWorkspaceStatus.Empty)
        {
            Clear();
            return;
        }

        Session.SetString(
            SessionKey,
            JsonSerializer.Serialize(snapshot, SerializerOptions));
    }
}
