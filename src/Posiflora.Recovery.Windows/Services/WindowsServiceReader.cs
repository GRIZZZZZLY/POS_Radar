using System.Management;

namespace Posiflora.Recovery.Windows.Services;

public sealed class WindowsServiceReader : IWindowsServiceReader
{
    public Task<WindowsServiceInfo?> GetServiceAsync(string serviceName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var escapedName = serviceName.Replace("'", "''", StringComparison.Ordinal);
        var query = $"SELECT Name, DisplayName, State, StartMode, PathName, ProcessId FROM Win32_Service WHERE Name = '{escapedName}'";

        using var searcher = new ManagementObjectSearcher(query);
        using var results = searcher.Get();

        foreach (ManagementObject service in results)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<WindowsServiceInfo?>(new WindowsServiceInfo(
                GetString(service, "Name"),
                GetString(service, "DisplayName"),
                GetString(service, "State"),
                GetString(service, "StartMode"),
                GetString(service, "PathName"),
                Convert.ToInt32(service["ProcessId"] ?? 0)));
        }

        return Task.FromResult<WindowsServiceInfo?>(null);
    }

    private static string GetString(ManagementObject service, string propertyName)
    {
        return Convert.ToString(service[propertyName]) ?? string.Empty;
    }
}
