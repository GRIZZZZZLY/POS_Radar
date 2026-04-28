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

    public async Task<IReadOnlyList<UemaSnapshot>> ReadDefaultAsync(CancellationToken cancellationToken)
    {
        return
        [
            await ReadAsync("uem-agent", "UEM Agent", cancellationToken),
            await ReadAsync("uem-updater", "UEM Updater", cancellationToken)
        ];
    }

    public async Task<UemaSnapshot> ReadAsync(string serviceName, string displayNameHint, CancellationToken cancellationToken)
    {
        var service = await serviceReader.GetServiceAsync(serviceName, cancellationToken);
        if (service is null)
        {
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

        var executablePath = ServiceExecutablePath.FromPathName(service.PathName);
        var binaryExists = fileProbe.Exists(executablePath);
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
