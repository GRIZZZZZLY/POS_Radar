using Posiflora.Recovery.Core.Diagnostics;

namespace Posiflora.Recovery.App.ViewModels;

public sealed class FindingViewModel(Finding finding)
{
    public string Id { get; } = finding.Id;

    public string Severity { get; } = finding.Severity.ToString();

    public string SeverityTitle { get; } = finding.Severity switch
    {
        FindingSeverity.Critical => "Критично",
        FindingSeverity.Warning => "Предупреждение",
        _ => "Информация"
    };

    public string SeverityBackground { get; } = finding.Severity switch
    {
        FindingSeverity.Critical => "#B42318",
        FindingSeverity.Warning => "#B54708",
        _ => "#2563EB"
    };

    public string Title { get; } = finding.Title;

    public string Evidence { get; } = finding.Evidence;

    public string Explanation { get; } = finding.Explanation;

    public string RecommendedAction { get; } = finding.RecommendedAction;

    public bool CanAutoFix { get; } = finding.CanAutoFix;

    public string Source { get; } = finding.Source;
}
