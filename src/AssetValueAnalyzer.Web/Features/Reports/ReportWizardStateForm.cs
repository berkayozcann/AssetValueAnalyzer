namespace AssetValueAnalyzer.Web.Features.Reports;

public sealed record ReportWizardStateForm(
    int Step,
    string? StartMonth,
    string? EndMonth);
