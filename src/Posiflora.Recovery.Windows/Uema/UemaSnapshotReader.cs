using Posiflora.Recovery.Core.Diagnostics;
using Posiflora.Recovery.Core.Uema;
using Posiflora.Recovery.Windows.Files;
using Posiflora.Recovery.Windows.Network;
using Posiflora.Recovery.Windows.Services;

namespace Posiflora.Recovery.Windows.Uema;

public sealed class UemaSnapshotReader(
    IWindowsServiceReader serviceReader,
    IFileProbe fileProbe,
    ITcpConnectionReader tcpConnectionReader)
{
    private static readonly string[] CloudConnectionStates = ["Established"];
    private static readonly int[] CloudPorts = [443, 1883, 8883];

    public Task<IReadOnlyList<UemaSnapshot>> ReadDefaultAsync(CancellationToken cancellationToken)
    {
        return ReadDefaultAsync(NullDiagnosticLogSink.Instance, cancellationToken);
    }

    public async Task<IReadOnlyList<UemaSnapshot>> ReadDefaultAsync(IDiagnosticLogSink log, CancellationToken cancellationToken)
    {
        return
        [
            await ReadAsync("uem-agent", "UEM Agent", log, cancellationToken),
            await ReadAsync("uem-updater", "UEM Updater", log, cancellationToken)
        ];
    }

    public Task<UemaSnapshot> ReadAsync(string serviceName, string displayNameHint, CancellationToken cancellationToken)
    {
        return ReadAsync(serviceName, displayNameHint, NullDiagnosticLogSink.Instance, cancellationToken);
    }

    public async Task<UemaSnapshot> ReadAsync(string serviceName, string displayNameHint, IDiagnosticLogSink log, CancellationToken cancellationToken)
    {
        log.Write(DiagnosticLogLevel.Info, "WMI", $"Чтение службы {serviceName}");
        var service = await serviceReader.GetServiceAsync(serviceName, cancellationToken);
        if (service is null)
        {
            log.Write(DiagnosticLogLevel.Error, "WMI", $"Служба {serviceName} не найдена");
            return new UemaSnapshot(
                serviceName,
                displayNameHint,
                false,
                "Missing",
                string.Empty,
                string.Empty,
                false,
                0,
                false,
                []);
        }

        log.Write(DiagnosticLogLevel.Success, "WMI", $"Служба {serviceName} найдена: состояние={service.State}, запуск={service.StartMode}, PID={service.ProcessId}");
        var executablePath = ServiceExecutablePath.FromPathName(service.PathName);
        log.Write(DiagnosticLogLevel.Info, "PathName", $"{serviceName}: {service.PathName} -> {executablePath}");

        var binaryExists = fileProbe.Exists(executablePath);
        log.Write(
            binaryExists ? DiagnosticLogLevel.Success : DiagnosticLogLevel.Error,
            "Файл",
            binaryExists ? $"{serviceName}: файл найден: {executablePath}" : $"{serviceName}: файл не найден: {executablePath}");

        if (service.ProcessId <= 0)
        {
            log.Write(DiagnosticLogLevel.Warning, "TCP", $"{serviceName}: процесс не запущен, TCP-соединения будут пустыми");
        }

        var connections = await tcpConnectionReader.GetConnectionsAsync(cancellationToken);
        var listeners = await tcpConnectionReader.GetListenersAsync(cancellationToken);

        var localPortsListening = listeners
            .Where(listener => BelongsToProcess(listener, service.ProcessId))
            .Select(listener => listener.LocalPort)
            .Distinct()
            .Order()
            .ToArray();

        var hasCloudConnection = connections.Any(connection =>
            BelongsToProcess(connection, service.ProcessId)
            && CloudConnectionStates.Contains(connection.State, StringComparer.OrdinalIgnoreCase)
            && CloudPorts.Contains(connection.RemotePort));

        log.Write(DiagnosticLogLevel.Info, "TCP", $"{serviceName}: соединений={connections.Count}, слушателей={listeners.Count}, локальные порты=[{string.Join(", ", localPortsListening)}]");
        log.Write(
            hasCloudConnection ? DiagnosticLogLevel.Success : DiagnosticLogLevel.Warning,
            "TCP",
            hasCloudConnection ? $"{serviceName}: облачное соединение найдено" : $"{serviceName}: облачное соединение не найдено");

        return new UemaSnapshot(
            service.Name,
            string.IsNullOrWhiteSpace(service.DisplayName) ? displayNameHint : service.DisplayName,
            true,
            service.State,
            service.StartMode,
            service.PathName,
            binaryExists,
            service.ProcessId,
            hasCloudConnection,
            localPortsListening);
    }

    private static bool BelongsToProcess(TcpConnectionInfo connection, int processId)
    {
        return processId > 0 && connection.ProcessId == processId;
    }
}
