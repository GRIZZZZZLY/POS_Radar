namespace Posiflora.Recovery.Core.Uema;

public sealed record UemaSnapshot(
    string ServiceName,
    string DisplayName,
    bool Exists,
    string Status,
    string StartMode,
    string PathName,
    bool BinaryExists,
    int ProcessId,
    bool HasCloudConnection,
    IReadOnlyList<int> LocalPortsListening);
