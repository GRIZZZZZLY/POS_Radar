using System.Collections.ObjectModel;
using Posiflora.Recovery.App.Services;
using Posiflora.Recovery.Core.Diagnostics;
using Posiflora.Recovery.Core.Uema;

namespace Posiflora.Recovery.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private DiagnosticProfileItemViewModel? _selectedProfile;
    private FindingViewModel? _selectedFinding;
    private string _statusText = "Ожидание диагностики";
    private string _summaryText = "Findings еще не загружены";

    public MainWindowViewModel()
    {
        var metadata = UemaDiagnosticProfile.Metadata;
        Profiles.Add(new DiagnosticProfileItemViewModel(metadata.Id, metadata.Title, metadata.Description));
        SelectedProfile = Profiles.FirstOrDefault();
    }

    public ObservableCollection<DiagnosticProfileItemViewModel> Profiles { get; } = [];

    public ObservableCollection<FindingViewModel> Findings { get; } = [];

    public DiagnosticProfileItemViewModel? SelectedProfile
    {
        get => _selectedProfile;
        set => SetProperty(ref _selectedProfile, value);
    }

    public FindingViewModel? SelectedFinding
    {
        get => _selectedFinding;
        set => SetProperty(ref _selectedFinding, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string SummaryText
    {
        get => _summaryText;
        private set => SetProperty(ref _summaryText, value);
    }

    public async Task LoadAsync(IDiagnosticsClient diagnosticsClient)
    {
        StatusText = "Читаю службы, файлы и TCP состояние";
        var result = await diagnosticsClient.RunUemaProfileAsync(CancellationToken.None);

        Findings.Clear();
        foreach (var finding in result.Findings.OrderByDescending(finding => finding.Severity))
        {
            Findings.Add(new FindingViewModel(finding));
        }

        SelectedFinding = Findings.FirstOrDefault();
        StatusText = $"Последний запуск: {result.FinishedAt.LocalDateTime:G}";
        SummaryText = BuildSummary(result.Findings);
    }

    private static string BuildSummary(IReadOnlyList<Finding> findings)
    {
        if (findings.Count == 0)
        {
            return "Критичных проблем не найдено";
        }

        var critical = findings.Count(finding => finding.Severity == FindingSeverity.Critical);
        var warnings = findings.Count(finding => finding.Severity == FindingSeverity.Warning);
        return $"Critical: {critical}; Warning: {warnings}; Total: {findings.Count}";
    }
}
