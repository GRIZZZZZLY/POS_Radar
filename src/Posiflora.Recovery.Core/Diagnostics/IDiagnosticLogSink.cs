namespace Posiflora.Recovery.Core.Diagnostics;

public interface IDiagnosticLogSink
{
    void Write(DiagnosticLogLevel level, string source, string message);
}
