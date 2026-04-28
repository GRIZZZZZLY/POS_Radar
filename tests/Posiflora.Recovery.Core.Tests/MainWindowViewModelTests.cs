using FluentAssertions;
using Posiflora.Recovery.App.Services;
using Posiflora.Recovery.App.ViewModels;
using Posiflora.Recovery.Core.Diagnostics;

namespace Posiflora.Recovery.Core.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public async Task Run_diagnostics_command_populates_russian_status_cards_and_log()
    {
        var finishedAt = new DateTimeOffset(2026, 4, 28, 10, 45, 0, TimeSpan.FromHours(3));
        var finding = new Finding(
            "uem.service.missing_binary",
            FindingSeverity.Critical,
            "uem-agent: бинарник службы отсутствует",
            "PathName=\"C:\\Program Files\\UEM\\Agent\\bin\\uema.exe\"",
            "Служба зарегистрирована, но исполняемый файл не найден.",
            "Переустановить UEMA/драйвер Posiflora.",
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
        viewModel.LogLines.Should().Contain(line => line.Contains("Диагностика завершена", StringComparison.Ordinal));
    }

    private sealed class FakeDiagnosticsClient(DateTimeOffset finishedAt, IReadOnlyList<Finding> findings) : IDiagnosticsClient
    {
        public Task<CheckResult> RunUemaProfileAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new CheckResult(
                "uema.error11",
                finishedAt.AddSeconds(-1),
                finishedAt,
                findings));
        }
    }
}
