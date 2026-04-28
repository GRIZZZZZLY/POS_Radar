using Posiflora.Recovery.Core.Diagnostics;

namespace Posiflora.Recovery.App.Services;

public interface IDiagnosticsClient
{
    Task<CheckResult> RunUemaProfileAsync(IDiagnosticLogSink log, CancellationToken cancellationToken);
}
