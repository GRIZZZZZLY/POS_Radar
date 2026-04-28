using Posiflora.Recovery.Core.Diagnostics;

namespace Posiflora.Recovery.App.Services;

public interface IDiagnosticsClient
{
    Task<DiagnosticRunResult> RunUemaProfileAsync(IDiagnosticLogSink log, CancellationToken cancellationToken);
}
