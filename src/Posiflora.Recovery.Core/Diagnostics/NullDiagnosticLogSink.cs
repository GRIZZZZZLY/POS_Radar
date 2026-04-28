namespace Posiflora.Recovery.Core.Diagnostics;

public sealed class NullDiagnosticLogSink : IDiagnosticLogSink
{
    public static NullDiagnosticLogSink Instance { get; } = new();

    private NullDiagnosticLogSink()
    {
    }

    public void Write(DiagnosticLogLevel level, string source, string message)
    {
    }
}
