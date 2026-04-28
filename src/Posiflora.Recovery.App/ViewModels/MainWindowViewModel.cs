using System.Collections.ObjectModel;
using Posiflora.Recovery.App.Services;
using Posiflora.Recovery.Core.Diagnostics;
using Posiflora.Recovery.Core.Uema;

namespace Posiflora.Recovery.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly IDiagnosticsClient _diagnosticsClient;
    private readonly ViewModelDiagnosticLogSink _logSink;
    private DiagnosticProfileItemViewModel? _selectedProfile;
    private FindingViewModel? _selectedFinding;
    private string _statusText = "Диагностика еще не запускалась";
    private string _summaryText = "Нажмите «Запустить диагностику»";
    private string _agentStatusText = "Локальный агент: не установлен как служба";
    private int _criticalCount;
    private int _warningCount;
    private int _totalCount;
    private bool _isBusy;

    public MainWindowViewModel()
        : this(new LocalDiagnosticsClient())
    {
    }

    public MainWindowViewModel(IDiagnosticsClient diagnosticsClient)
    {
        _diagnosticsClient = diagnosticsClient;
        _logSink = new ViewModelDiagnosticLogSink(AppendLog);
        RunDiagnosticsCommand = new AsyncRelayCommand(RunDiagnosticsAsync, () => !IsBusy);

        var metadata = UemaDiagnosticProfile.Metadata;
        Profiles.Add(new DiagnosticProfileItemViewModel(metadata.Id, metadata.Title, metadata.Description));
        SelectedProfile = Profiles.FirstOrDefault();
        AppendLog(DiagnosticLogLevel.Info, "Система", "Готово к запуску диагностики.");
    }

    public ObservableCollection<DiagnosticProfileItemViewModel> Profiles { get; } = [];

    public ObservableCollection<FindingViewModel> Findings { get; } = [];

    public ObservableCollection<DiagnosticLogEntryViewModel> LogEntries { get; } = [];

    public AsyncRelayCommand RunDiagnosticsCommand { get; }

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

    public string AgentStatusText
    {
        get => _agentStatusText;
        private set => SetProperty(ref _agentStatusText, value);
    }

    public int CriticalCount
    {
        get => _criticalCount;
        private set => SetProperty(ref _criticalCount, value);
    }

    public int WarningCount
    {
        get => _warningCount;
        private set => SetProperty(ref _warningCount, value);
    }

    public int TotalCount
    {
        get => _totalCount;
        private set => SetProperty(ref _totalCount, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RunDiagnosticsCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public async Task RunDiagnosticsAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        AppendLog(DiagnosticLogLevel.Info, "Профиль", "Запуск профиля: UEMA / ошибка 11");
        StatusText = "Читаю службы, файлы и TCP-соединения";

        try
        {
            var result = await _diagnosticsClient.RunUemaProfileAsync(_logSink, CancellationToken.None);
            ApplyResult(result);
            AppendLog(DiagnosticLogLevel.Success, "Профиль", $"Диагностика завершена. Найдено проблем: {result.Findings.Count}.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public Task LoadAsync()
    {
        return RunDiagnosticsAsync();
    }

    private void ApplyResult(CheckResult result)
    {
        Findings.Clear();
        foreach (var finding in result.Findings.OrderByDescending(finding => finding.Severity))
        {
            Findings.Add(new FindingViewModel(finding));
        }

        CriticalCount = result.Findings.Count(finding => finding.Severity == FindingSeverity.Critical);
        WarningCount = result.Findings.Count(finding => finding.Severity == FindingSeverity.Warning);
        TotalCount = result.Findings.Count;
        SelectedFinding = Findings.FirstOrDefault();
        StatusText = $"Последний запуск: {result.FinishedAt.LocalDateTime:G}";
        SummaryText = BuildSummary(result.Findings);
        AgentStatusText = "Локальный агент: контрольный сигнал";
    }

    private void AppendLog(DiagnosticLogLevel level, string source, string message)
    {
        var entry = new DiagnosticLogEntryViewModel(new DiagnosticLogEntry(DateTimeOffset.Now, level, source, message));
        ApplicationLogStore.Append(entry.ToLogLine());
        LogEntries.Insert(0, entry);

        while (LogEntries.Count > 120)
        {
            LogEntries.RemoveAt(LogEntries.Count - 1);
        }
    }

    private static string BuildSummary(IReadOnlyList<Finding> findings)
    {
        if (findings.Count == 0)
        {
            return "Критических проблем не найдено";
        }

        var critical = findings.Count(finding => finding.Severity == FindingSeverity.Critical);
        var warnings = findings.Count(finding => finding.Severity == FindingSeverity.Warning);
        return $"Критические: {critical}; Предупреждения: {warnings}; Всего: {findings.Count}";
    }

    private sealed class ViewModelDiagnosticLogSink(Action<DiagnosticLogLevel, string, string> append) : IDiagnosticLogSink
    {
        public void Write(DiagnosticLogLevel level, string source, string message)
        {
            append(level, source, message);
        }
    }
}
