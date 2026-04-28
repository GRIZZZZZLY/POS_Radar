namespace Posiflora.Recovery.Windows.Services;

public sealed record WindowsServiceInfo(
    string Name,
    string DisplayName,
    string State,
    string StartMode,
    string PathName,
    int ProcessId);
