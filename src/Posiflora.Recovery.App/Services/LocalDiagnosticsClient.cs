using Posiflora.Recovery.Core.Diagnostics;
using Posiflora.Recovery.Core.Uema;
using Posiflora.Recovery.Windows.Files;
using Posiflora.Recovery.Windows.Network;
using Posiflora.Recovery.Windows.Services;
using Posiflora.Recovery.Windows.Uema;

namespace Posiflora.Recovery.App.Services;

public sealed class LocalDiagnosticsClient : IDiagnosticsClient
{
    public async Task<CheckResult> RunUemaProfileAsync(CancellationToken cancellationToken)
    {
        try
        {
            var reader = new UemaSnapshotReader(
                new WindowsServiceReader(),
                new FileProbe(),
                new TcpConnectionReader());

            var snapshots = await reader.ReadDefaultAsync(cancellationToken);
            return UemaDiagnosticProfile.Build(snapshots);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
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
}
