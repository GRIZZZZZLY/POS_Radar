namespace Posiflora.Recovery.Windows.Uema;

public static class ServiceExecutablePath
{
    public static string FromPathName(string pathName)
    {
        if (string.IsNullOrWhiteSpace(pathName))
        {
            return string.Empty;
        }

        var trimmed = pathName.Trim();
        if (trimmed.StartsWith('"'))
        {
            var closingQuoteIndex = trimmed.IndexOf('"', 1);
            return closingQuoteIndex > 1
                ? trimmed[1..closingQuoteIndex]
                : trimmed.Trim('"');
        }

        var exeIndex = trimmed.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exeIndex >= 0)
        {
            return trimmed[..(exeIndex + 4)];
        }

        var firstSpaceIndex = trimmed.IndexOf(' ');
        return firstSpaceIndex >= 0 ? trimmed[..firstSpaceIndex] : trimmed;
    }
}
