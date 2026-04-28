namespace Posiflora.Recovery.Windows.Services;

public interface IWindowsServiceReader
{
    Task<WindowsServiceInfo?> GetServiceAsync(string serviceName, CancellationToken cancellationToken);
}
