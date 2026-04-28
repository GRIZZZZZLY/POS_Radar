namespace Posiflora.Recovery.Core.Diagnostics;

public sealed record RemediationAction(
    string Id,
    string Title,
    string Description,
    bool RequiresConfirmation);
