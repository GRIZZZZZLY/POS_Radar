using System.Text;
using System.IO;
using Posiflora.Recovery.App.ViewModels;

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

    public static string ExportReport(IEnumerable<FindingViewModel> findings)
    {
        Directory.CreateDirectory(DirectoryPath);
        var reportsDirectory = Path.Combine(DirectoryPath, "reports");
        Directory.CreateDirectory(reportsDirectory);

        var reportPath = Path.Combine(reportsDirectory, $"pos-radar-report-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.txt");
        var builder = new StringBuilder();
        builder.AppendLine("POS Radar - отчет диагностики");
        builder.AppendLine($"Сформирован: {DateTimeOffset.Now:dd.MM.yyyy HH:mm:ss}");
        builder.AppendLine();

        foreach (var finding in findings)
        {
            builder.AppendLine($"[{finding.SeverityTitle}] {finding.Title}");
            builder.AppendLine($"Источник: {finding.Source}");
            builder.AppendLine($"Данные: {finding.Evidence}");
            builder.AppendLine($"Рекомендация: {finding.RecommendedAction}");
            builder.AppendLine();
        }

        File.WriteAllText(reportPath, builder.ToString(), Encoding.UTF8);
        return reportPath;
    }
}
