namespace Posiflora.Recovery.Windows.Files;

public interface IFileProbe
{
    bool Exists(string path);
}
