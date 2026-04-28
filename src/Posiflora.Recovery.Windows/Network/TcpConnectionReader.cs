using System.Net.NetworkInformation;

namespace Posiflora.Recovery.Windows.Network;

public sealed class TcpConnectionReader : ITcpConnectionReader
{
    public Task<IReadOnlyList<TcpConnectionInfo>> GetConnectionsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var properties = IPGlobalProperties.GetIPGlobalProperties();
        var connections = properties.GetActiveTcpConnections()
            .Select(connection => new TcpConnectionInfo(
                0,
                connection.LocalEndPoint.Port,
                connection.RemoteEndPoint.Address.ToString(),
                connection.RemoteEndPoint.Port,
                connection.State.ToString()))
            .ToArray();

        return Task.FromResult<IReadOnlyList<TcpConnectionInfo>>(connections);
    }

    public Task<IReadOnlyList<TcpConnectionInfo>> GetListenersAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var properties = IPGlobalProperties.GetIPGlobalProperties();
        var listeners = properties.GetActiveTcpListeners()
            .Select(listener => new TcpConnectionInfo(
                0,
                listener.Port,
                string.Empty,
                0,
                "Listen"))
            .ToArray();

        return Task.FromResult<IReadOnlyList<TcpConnectionInfo>>(listeners);
    }
}
