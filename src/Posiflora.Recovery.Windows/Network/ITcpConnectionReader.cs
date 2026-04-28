namespace Posiflora.Recovery.Windows.Network;

public interface ITcpConnectionReader
{
    Task<IReadOnlyList<TcpConnectionInfo>> GetConnectionsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<TcpConnectionInfo>> GetListenersAsync(CancellationToken cancellationToken);
}
