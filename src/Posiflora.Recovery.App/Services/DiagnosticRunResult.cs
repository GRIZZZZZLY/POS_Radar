using Posiflora.Recovery.Core.Diagnostics;
using Posiflora.Recovery.Core.Uema;

namespace Posiflora.Recovery.App.Services;

public sealed record DiagnosticRunResult(
    CheckResult CheckResult,
    IReadOnlyList<UemaSnapshot> UemaSnapshots);
