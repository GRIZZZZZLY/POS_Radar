using Posiflora.Recovery.Core.Diagnostics;
using Posiflora.Recovery.Core.Uema;
using Posiflora.Recovery.Windows.Files;
using Posiflora.Recovery.Windows.Network;
using Posiflora.Recovery.Windows.Services;
using Posiflora.Recovery.Windows.Uema;

namespace Posiflora.Recovery.App.Services;

public sealed class LocalDiagnosticsClient : IDiagnosticsClient
{
    public async Task<CheckResult> RunUemaProfileAsync(IDiagnosticLogSink log, CancellationToken cancellationToken)
    {
        try
        {
            log.Write(DiagnosticLogLevel.Info, "Профиль", "Подготовка адаптеров Windows: WMI, файловая система, TCP");
            var reader = new UemaSnapshotReader(
                new WindowsServiceReader(),
                new FileProbe(),
                new TcpConnectionReader());

            var snapshots = await reader.ReadDefaultAsync(log, cancellationToken);
            log.Write(DiagnosticLogLevel.Info, "Правила", "Построение findings по снимкам uem-agent и uem-updater");
            var result = UemaDiagnosticProfile.Build(snapshots);

            foreach (var finding in result.Findings)
            {
                var level = finding.Severity == FindingSeverity.Critical
                    ? DiagnosticLogLevel.Error
                    : DiagnosticLogLevel.Warning;
                log.Write(level, "Правила", $"Результат правила: id={finding.Id}; уровень={TranslateSeverity(finding.Severity)}; автоисправление={TranslateBoolean(finding.CanAutoFix)}; источник={finding.Source}");
            }

            log.Write(DiagnosticLogLevel.Success, "Профиль", $"Профиль завершен: критические={result.Findings.Count(finding => finding.Severity == FindingSeverity.Critical)}, предупреждения={result.Findings.Count(finding => finding.Severity == FindingSeverity.Warning)}");
            return result;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            log.Write(DiagnosticLogLevel.Error, "Профиль", $"Сбой локальной диагностики: {exception.Message}");
            var now = DateTimeOffset.UtcNow;
            return new CheckResult(
                UemaDiagnosticProfile.Metadata.Id,
                now,
                now,
                [
                    new Finding(
                        "uema.local_probe.failed",
                        FindingSeverity.Critical,
                        "Не удалось выполнить локальную диагностику UEMA",
                        exception.Message,
                        "Локальная проверка в режиме только чтения завершилась ошибкой до получения снимка состояния.",
                        "Запустить приложение от имени пользователя с доступом к WMI/CIM и повторить диагностику.",
                        false,
                        [],
                        "локальная диагностика")
                ]);
        }
    }

    private static string TranslateSeverity(FindingSeverity severity)
    {
        return severity switch
        {
            FindingSeverity.Critical => "критично",
            FindingSeverity.Warning => "предупреждение",
            _ => "информация"
        };
    }

    private static string TranslateBoolean(bool value)
    {
        return value ? "да" : "нет";
    }
}
