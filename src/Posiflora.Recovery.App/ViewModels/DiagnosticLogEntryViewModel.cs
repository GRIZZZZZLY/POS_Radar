using Posiflora.Recovery.Core.Diagnostics;

namespace Posiflora.Recovery.App.ViewModels;

public sealed class DiagnosticLogEntryViewModel(DiagnosticLogEntry entry)
{
    public DiagnosticLogLevel Level { get; } = entry.Level;

    public string Time { get; } = entry.Timestamp.LocalDateTime.ToString("HH:mm:ss");

    public string LevelTitle { get; } = entry.Level switch
    {
        DiagnosticLogLevel.Success => "ОК",
        DiagnosticLogLevel.Warning => "ПРЕД",
        DiagnosticLogLevel.Error => "ОШИБ",
        _ => "ИНФО"
    };

    public string LevelBrush { get; } = entry.Level switch
    {
        DiagnosticLogLevel.Success => "#86EFAC",
        DiagnosticLogLevel.Warning => "#FCD34D",
        DiagnosticLogLevel.Error => "#FCA5A5",
        _ => "#93C5FD"
    };

    public string Source { get; } = entry.Source;

    public string Message { get; } = entry.Message;

    public string ToLogLine()
    {
        return $"{Time}  {LevelTitle,-5}  {Source,-10}  {Message}";
    }
}
