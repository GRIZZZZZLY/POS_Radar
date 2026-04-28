namespace Posiflora.Recovery.Core.Diagnostics;

public sealed record DiagnosticLogEntry(
    DateTimeOffset Timestamp,
    DiagnosticLogLevel Level,
    string Source,
    string Message);
