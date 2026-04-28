using FluentAssertions;
using Posiflora.Recovery.App.Services;
using Posiflora.Recovery.App.ViewModels;
using Posiflora.Recovery.Core.Diagnostics;
using Posiflora.Recovery.Core.Uema;

namespace Posiflora.Recovery.Core.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public async Task Run_diagnostics_command_populates_summary_log_and_snapshot()
    {
        var finishedAt = new DateTimeOffset(2026, 4, 28, 10, 45, 0, TimeSpan.FromHours(3));
        var finding = new Finding(
            "uem.service.missing_binary",
            FindingSeverity.Critical,
            "uem-agent: binary missing",
            "PathName=\"C:\\Program Files\\UEM\\Agent\\bin\\uema.exe\"",
            "Service exists, but binary file is missing.",
            "Reinstall UEMA driver.",
            false,
            [],
            "uem-agent");

        var viewModel = new MainWindowViewModel(new FakeDiagnosticsClient(finishedAt, [finding]));

        await viewModel.RunDiagnosticsAsync();

        viewModel.CriticalCount.Should().Be(1);
        viewModel.WarningCount.Should().Be(0);
        viewModel.Findings.Should().ContainSingle();
        viewModel.SummaryText.Should().Be("Критические: 1; Предупреждения: 0; Всего: 1");
        viewModel.StatusText.Should().Contain("Последний запуск");
        viewModel.LogEntries.Should().Contain(entry => entry.Message.Contains("Диагностика завершена", StringComparison.Ordinal));
        viewModel.LogEntries.Should().Contain(entry => entry.Source == "WMI" && entry.Message.Contains("uem-agent", StringComparison.Ordinal));
        viewModel.LogEntries.Should().Contain(entry => entry.Source == "Файл" && entry.Level == DiagnosticLogLevel.Error);
        viewModel.UemaSnapshots.Should().ContainSingle(snapshot => snapshot.ServiceName == "uem-agent");
    }

    private sealed class FakeDiagnosticsClient(DateTimeOffset finishedAt, IReadOnlyList<Finding> findings) : IDiagnosticsClient
    {
        public Task<DiagnosticRunResult> RunUemaProfileAsync(IDiagnosticLogSink log, CancellationToken cancellationToken)
        {
            log.Write(DiagnosticLogLevel.Info, "WMI", "Чтение службы uem-agent");
            log.Write(DiagnosticLogLevel.Error, "Файл", "Файл не найден: C:\\Program Files\\UEM\\Agent\\bin\\uema.exe");

            var checkResult = new CheckResult(
                "uema.error11",
                finishedAt.AddSeconds(-1),
                finishedAt,
                findings);

            var snapshots = new[]
            {
                new UemaSnapshot(
                    "uem-agent",
                    "UEM Agent",
                    true,
                    "Stopped",
                    "Auto",
                    "\"C:\\Program Files\\UEM\\Agent\\bin\\uema.exe\"",
                    false,
                    0,
                    false,
                    [])
            };

            return Task.FromResult(new DiagnosticRunResult(checkResult, snapshots));
        }
    }
}
