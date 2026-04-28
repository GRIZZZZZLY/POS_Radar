using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Posiflora.Recovery.App.ViewModels;
using Posiflora.Recovery.Core.Uema;

namespace Posiflora.Recovery.App.Services;

public static class ApplicationLogStore
{
    public static string DirectoryPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "POS Radar",
        "logs");

    public static string CurrentLogPath { get; } = Path.Combine(DirectoryPath, "pos-radar.log");

    public static void Append(string line)
    {
        Directory.CreateDirectory(DirectoryPath);
        File.AppendAllText(CurrentLogPath, line + Environment.NewLine, Encoding.UTF8);
    }

    public static string ExportReport(
        IEnumerable<FindingViewModel> findings,
        IEnumerable<UemaSnapshot> snapshots,
        IEnumerable<DiagnosticLogEntryViewModel> logEntries)
    {
        Directory.CreateDirectory(DirectoryPath);
        var reportsDirectory = Path.Combine(DirectoryPath, "reports");
        Directory.CreateDirectory(reportsDirectory);

        var reportPath = Path.Combine(reportsDirectory, $"pos-radar-report-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.txt");
        var reportText = BuildReportText(
            findings,
            snapshots,
            logEntries,
            DateTimeOffset.Now,
            Environment.MachineName,
            RuntimeInformation.OSDescription);

        File.WriteAllText(reportPath, reportText, Encoding.UTF8);
        return reportPath;
    }

    public static string BuildReportText(
        IEnumerable<FindingViewModel> findings,
        IEnumerable<UemaSnapshot> snapshots,
        IEnumerable<DiagnosticLogEntryViewModel> logEntries,
        DateTimeOffset generatedAt,
        string machineName,
        string osDescription)
    {
        var builder = new StringBuilder();
        builder.AppendLine("POS Radar - отчет диагностики");
        builder.AppendLine($"Сформирован: {generatedAt:dd.MM.yyyy HH:mm:ss}");
        builder.AppendLine($"Компьютер: {machineName}");
        builder.AppendLine($"ОС: {osDescription}");
        builder.AppendLine();

        builder.AppendLine("Findings");
        foreach (var finding in findings)
        {
            builder.AppendLine($"[{finding.SeverityTitle}] {finding.Title}");
            builder.AppendLine($"Источник: {finding.Source}");
            builder.AppendLine($"Данные: {finding.Evidence}");
            builder.AppendLine($"Пояснение: {finding.Explanation}");
            builder.AppendLine($"Рекомендация: {finding.RecommendedAction}");
            builder.AppendLine($"Автоисправление: {FormatBool(finding.CanAutoFix)}");
            builder.AppendLine();
        }

        builder.AppendLine("Снимок UEMA");
        foreach (var snapshot in snapshots)
        {
            builder.AppendLine($"Служба: {snapshot.ServiceName}");
            builder.AppendLine($"Отображаемое имя: {snapshot.DisplayName}");
            builder.AppendLine($"Существует: {FormatBool(snapshot.Exists)}");
            builder.AppendLine($"Статус: {snapshot.Status}");
            builder.AppendLine($"Тип запуска: {snapshot.StartMode}");
            builder.AppendLine($"PathName: {snapshot.PathName}");
            builder.AppendLine($"Файл найден: {FormatBool(snapshot.BinaryExists)}");
            builder.AppendLine($"PID: {snapshot.ProcessId}");
            builder.AppendLine($"Облачное соединение: {FormatBool(snapshot.HasCloudConnection)}");
            builder.AppendLine($"Локальные listening-порты: {string.Join(", ", snapshot.LocalPortsListening)}");
            builder.AppendLine();
        }

        builder.AppendLine("Логи диагностики");
        foreach (var entry in logEntries)
        {
            builder.AppendLine(entry.ToLogLine());
        }

        return builder.ToString();
    }

    private static string FormatBool(bool value)
    {
        return value ? "да" : "нет";
    }
}
