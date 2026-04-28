namespace Posiflora.Recovery.Windows.Network;

public sealed record TcpConnectionInfo(
    int ProcessId,
    int LocalPort,
    string RemoteAddress,
    int RemotePort,
    string State);
