namespace Posiflora.Recovery.Agent;

public sealed record AgentHeartbeat(
    string MachineName,
    int ProcessId);
