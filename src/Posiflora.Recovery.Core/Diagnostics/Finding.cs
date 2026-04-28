namespace Posiflora.Recovery.Core.Diagnostics;

public sealed record Finding(
    string Id,
    FindingSeverity Severity,
    string Title,
    string Evidence,
    string Explanation,
    string RecommendedAction,
    bool CanAutoFix,
    IReadOnlyList<RemediationAction> Actions,
    string Source);
