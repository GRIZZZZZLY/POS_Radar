using Posiflora.Recovery.Core.Diagnostics;

namespace Posiflora.Recovery.Core.Uema;

public static class UemaFindingMapper
{
    public static IReadOnlyList<Finding> Map(UemaSnapshot snapshot)
    {
        if (!snapshot.Exists)
        {
            return
            [
                new Finding(
                    "uem.service.missing",
                    FindingSeverity.Critical,
                    $"{snapshot.ServiceName}: служба отсутствует",
                    $"ServiceName={snapshot.ServiceName}; DisplayName={snapshot.DisplayName}",
                    "Windows не видит службу UEMA. Автоматический запуск невозможен, пока служба не установлена.",
                    "Переустановить UEMA/драйвер Posiflora или восстановить службу штатным инсталлятором.",
                    false,
                    [],
                    snapshot.ServiceName)
            ];
        }

        if (!snapshot.BinaryExists)
        {
            return
            [
                new Finding(
                    "uem.service.missing_binary",
                    FindingSeverity.Critical,
                    $"{snapshot.ServiceName}: бинарник службы отсутствует",
                    $"ServiceName={snapshot.ServiceName}; Status={snapshot.Status}; PathName={snapshot.PathName}",
                    "Служба зарегистрирована в Windows, но исполняемый файл из PathName не найден. Это важнее, чем обычный Stopped: Start-Service не восстановит отсутствующий файл.",
                    "Переустановить UEMA/драйвер Posiflora, затем повторить диагностику.",
                    false,
                    [],
                    snapshot.ServiceName)
            ];
        }

        if (!IsRunning(snapshot.Status))
        {
            return
            [
                new Finding(
                    "uem.service.stopped",
                    FindingSeverity.Warning,
                    $"{snapshot.ServiceName}: служба остановлена",
                    $"ServiceName={snapshot.ServiceName}; Status={snapshot.Status}; StartMode={snapshot.StartMode}; PathName={snapshot.PathName}",
                    "Бинарник найден, поэтому проблема похожа на остановленную службу, а не на поврежденную установку.",
                    "Запустить службу и проверить повторно.",
                    true,
                    [new RemediationAction($"service.start.{snapshot.ServiceName}", "Запустить службу", $"Start-Service {snapshot.ServiceName}", true)],
                    snapshot.ServiceName)
            ];
        }

        if (!snapshot.HasCloudConnection)
        {
            return
            [
                new Finding(
                    "uem.cloud.no_connection",
                    FindingSeverity.Warning,
                    $"{snapshot.ServiceName}: нет cloud connection",
                    $"ServiceName={snapshot.ServiceName}; ProcessId={snapshot.ProcessId}; LocalPortsListening={string.Join(',', snapshot.LocalPortsListening)}",
                    "Служба запущена, но активного исходящего соединения с облаком не обнаружено.",
                    "Проверить интернет, DNS, firewall/proxy и доступность облачных endpoint UEMA.",
                    false,
                    [],
                    snapshot.ServiceName)
            ];
        }

        return [];
    }

    private static bool IsRunning(string status)
    {
        return string.Equals(status, "Running", StringComparison.OrdinalIgnoreCase);
    }
}
