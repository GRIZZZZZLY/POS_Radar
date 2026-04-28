namespace Posiflora.Recovery.Core.Diagnostics;

public sealed record CheckResult(
    string CheckId,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    IReadOnlyList<Finding> Findings)
{
    public bool HasCriticalFindings => Findings.Any(finding => finding.Severity == FindingSeverity.Critical);
}
