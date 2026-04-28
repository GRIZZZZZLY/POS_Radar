using FluentAssertions;
using Posiflora.Recovery.App.Services;
using Posiflora.Recovery.App.ViewModels;
using Posiflora.Recovery.Core.Diagnostics;
using Posiflora.Recovery.Core.Uema;

namespace Posiflora.Recovery.Core.Tests;

public sealed class ApplicationLogStoreTests
{
    [Fact]
    public void Builds_report_with_machine_info_uema_snapshot_findings_and_logs()
    {
        var finding = new FindingViewModel(new Finding(
            "uem.service.missing_binary",
            FindingSeverity.Critical,
            "uem-agent: бинарник службы отсутствует",
            "PathName=\"C:\\Program Files\\UEM\\Agent\\bin\\uema.exe\"",
            "Служба есть, файл отсутствует.",
            "Переустановить UEMA.",
            false,
            [],
            "uem-agent"));

        var snapshot = new UemaSnapshot(
            "uem-agent",
            "UEM Agent",
            true,
            "Stopped",
            "Auto",
            "\"C:\\Program Files\\UEM\\Agent\\bin\\uema.exe\"",
            false,
            0,
            false,
            []);

        var logEntry = new DiagnosticLogEntryViewModel(new DiagnosticLogEntry(
            new DateTimeOffset(2026, 4, 28, 11, 10, 0, TimeSpan.FromHours(3)),
            DiagnosticLogLevel.Error,
            "Файл",
            "uem-agent: файл не найден"));

        var report = ApplicationLogStore.BuildReportText(
            [finding],
            [snapshot],
            [logEntry],
            new DateTimeOffset(2026, 4, 28, 11, 11, 0, TimeSpan.FromHours(3)),
            "TEST-PC",
            "Windows 11");

        report.Should().Contain("Компьютер: TEST-PC");
        report.Should().Contain("ОС: Windows 11");
        report.Should().Contain("Снимок UEMA");
        report.Should().Contain("Служба: uem-agent");
        report.Should().Contain("Файл найден: нет");
        report.Should().Contain("uem-agent: бинарник службы отсутствует");
        report.Should().Contain("uem-agent: файл не найден");
    }
}
