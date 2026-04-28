namespace Posiflora.Recovery.App.ViewModels;

public sealed class DiagnosticProfileItemViewModel(string id, string title, string description)
{
    public string Id { get; } = id;

    public string Title { get; } = title;

    public string Description { get; } = description;
}
