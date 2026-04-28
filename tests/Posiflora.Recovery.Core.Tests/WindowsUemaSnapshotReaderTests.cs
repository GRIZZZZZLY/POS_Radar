using FluentAssertions;
using Posiflora.Recovery.Windows.Files;
using Posiflora.Recovery.Windows.Network;
using Posiflora.Recovery.Windows.Services;
using Posiflora.Recovery.Windows.Uema;

namespace Posiflora.Recovery.Core.Tests;

public sealed class WindowsUemaSnapshotReaderTests
{
    [Fact]
    public void Extracts_binary_path_from_quoted_service_path_with_arguments()
    {
        var path = ServiceExecutablePath.FromPathName("\"C:\\Program Files\\UEM\\uema.exe\" --service");

        path.Should().Be("C:\\Program Files\\UEM\\uema.exe");
    }

    [Fact]
    public async Task Builds_missing_service_snapshot_without_file_or_network_checks()
    {
        var reader = new UemaSnapshotReader(
            new FakeServiceReader((WindowsServiceInfo?)null),
            new RecordingFileProbe(),
            new FakeTcpConnectionReader());

        var snapshot = await reader.ReadAsync("uem-agent", "UEM Agent", CancellationToken.None);

        snapshot.Exists.Should().BeFalse();
        snapshot.BinaryExists.Should().BeFalse();
        snapshot.HasCloudConnection.Should().BeFalse();
        snapshot.ServiceName.Should().Be("uem-agent");
    }

    [Fact]
    public async Task Builds_snapshot_with_binary_and_cloud_connection()
    {
        var service = new WindowsServiceInfo(
            "uem-agent",
            "UEM Agent",
            "Running",
            "Auto",
            "\"C:\\Program Files\\UEM\\uema.exe\" --service",
            42);

        var reader = new UemaSnapshotReader(
            new FakeServiceReader(service),
            new RecordingFileProbe(existingPath: "C:\\Program Files\\UEM\\uema.exe"),
            new FakeTcpConnectionReader(
                connections: [new TcpConnectionInfo(42, 50123, "203.0.113.10", 443, "Established")],
                listeners: [new TcpConnectionInfo(42, 5050, "0.0.0.0", 0, "Listen")]));

        var snapshot = await reader.ReadAsync("uem-agent", "UEM Agent", CancellationToken.None);

        snapshot.Exists.Should().BeTrue();
        snapshot.BinaryExists.Should().BeTrue();
        snapshot.HasCloudConnection.Should().BeTrue();
        snapshot.LocalPortsListening.Should().Contain(5050);
    }

    [Fact]
    public async Task Reads_default_agent_and_updater_services()
    {
        var reader = new UemaSnapshotReader(
            new FakeServiceReader(new Dictionary<string, WindowsServiceInfo>
            {
                ["uem-agent"] = CreateService("uem-agent", "Running", 10),
                ["uem-updater"] = CreateService("uem-updater", "Stopped", 11)
            }),
            new RecordingFileProbe(existingPath: "C:\\Program Files\\UEM\\uema.exe"),
            new FakeTcpConnectionReader());

        var snapshots = await reader.ReadDefaultAsync(CancellationToken.None);

        snapshots.Should().HaveCount(2);
        snapshots.Select(snapshot => snapshot.ServiceName).Should().BeEquivalentTo("uem-agent", "uem-updater");
    }

    private static WindowsServiceInfo CreateService(string serviceName, string state, int processId)
    {
        return new WindowsServiceInfo(
            serviceName,
            serviceName,
            state,
            "Auto",
            "\"C:\\Program Files\\UEM\\uema.exe\"",
            processId);
    }

    private sealed class FakeServiceReader : IWindowsServiceReader
    {
        private readonly IReadOnlyDictionary<string, WindowsServiceInfo> _services;

        public FakeServiceReader(WindowsServiceInfo? service)
        {
            _services = service is null
                ? new Dictionary<string, WindowsServiceInfo>()
                : new Dictionary<string, WindowsServiceInfo> { [service.Name] = service };
        }

        public FakeServiceReader(IReadOnlyDictionary<string, WindowsServiceInfo> services)
        {
            _services = services;
        }

        public Task<WindowsServiceInfo?> GetServiceAsync(string serviceName, CancellationToken cancellationToken)
        {
            _services.TryGetValue(serviceName, out var service);
            return Task.FromResult(service);
        }
    }

    private sealed class RecordingFileProbe(string? existingPath = null) : IFileProbe
    {
        public bool Exists(string path)
        {
            return string.Equals(path, existingPath, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class FakeTcpConnectionReader(
        IReadOnlyList<TcpConnectionInfo>? connections = null,
        IReadOnlyList<TcpConnectionInfo>? listeners = null) : ITcpConnectionReader
    {
        public Task<IReadOnlyList<TcpConnectionInfo>> GetConnectionsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(connections ?? []);
        }

        public Task<IReadOnlyList<TcpConnectionInfo>> GetListenersAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(listeners ?? []);
        }
    }
}
