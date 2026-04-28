namespace Posiflora.Recovery.Windows.Files;

public sealed class FileProbe : IFileProbe
{
    public bool Exists(string path)
    {
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
    }
}
