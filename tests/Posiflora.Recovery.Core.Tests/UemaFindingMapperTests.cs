using FluentAssertions;
using Posiflora.Recovery.Core.Diagnostics;
using Posiflora.Recovery.Core.Uema;

namespace Posiflora.Recovery.Core.Tests;

public sealed class UemaFindingMapperTests
{
    [Fact]
    public void Maps_missing_binary_as_critical_not_auto_fixable()
    {
        var snapshot = CreateSnapshot(binaryExists: false, status: "Stopped");

        var findings = UemaFindingMapper.Map(snapshot);

        findings.Should().ContainSingle();
        findings[0].Id.Should().Be("uem.service.missing_binary");
        findings[0].Severity.Should().Be(FindingSeverity.Critical);
        findings[0].CanAutoFix.Should().BeFalse();
        findings[0].Evidence.Should().Contain("uema.exe");
    }

    [Fact]
    public void Maps_stopped_service_with_existing_binary_as_repairable()
    {
        var snapshot = CreateSnapshot(status: "Stopped");

        var findings = UemaFindingMapper.Map(snapshot);

        findings.Should().ContainSingle();
        findings[0].Id.Should().Be("uem.service.stopped");
        findings[0].Severity.Should().Be(FindingSeverity.Warning);
        findings[0].CanAutoFix.Should().BeTrue();
        findings[0].Actions.Should().ContainSingle(action => action.Id == "service.start.uem-agent");
    }

    [Fact]
    public void Maps_missing_service_as_critical()
    {
        var snapshot = CreateSnapshot(exists: false, pathName: string.Empty, binaryExists: false);

        var findings = UemaFindingMapper.Map(snapshot);

        findings.Should().ContainSingle();
        findings[0].Id.Should().Be("uem.service.missing");
        findings[0].Severity.Should().Be(FindingSeverity.Critical);
        findings[0].CanAutoFix.Should().BeFalse();
    }

    [Fact]
    public void Maps_running_service_without_cloud_connection_as_warning()
    {
        var snapshot = CreateSnapshot(status: "Running", hasCloudConnection: false);

        var findings = UemaFindingMapper.Map(snapshot);

        findings.Should().ContainSingle();
        findings[0].Id.Should().Be("uem.cloud.no_connection");
        findings[0].Severity.Should().Be(FindingSeverity.Warning);
        findings[0].CanAutoFix.Should().BeFalse();
    }

    [Fact]
    public void Builds_uema_profile_for_agent_and_updater()
    {
        var snapshots = new[]
        {
            CreateSnapshot(serviceName: "uem-agent", status: "Running", hasCloudConnection: false),
            CreateSnapshot(serviceName: "uem-updater", displayName: "UEM Updater", status: "Stopped")
        };

        var profile = UemaDiagnosticProfile.Build(snapshots);

        profile.CheckId.Should().Be("uema.error11");
        profile.Findings.Should().Contain(finding => finding.Source == "uem-agent");
        profile.Findings.Should().Contain(finding => finding.Source == "uem-updater");
    }

    private static UemaSnapshot CreateSnapshot(
        string serviceName = "uem-agent",
        string displayName = "UEM Agent",
        bool exists = true,
        string status = "Running",
        string startMode = "Auto",
        string pathName = "\"C:\\Program Files\\UEM\\uema.exe\"",
        bool binaryExists = true,
        int processId = 1444,
        bool hasCloudConnection = true,
        IReadOnlyList<int>? localPortsListening = null)
    {
        return new UemaSnapshot(
            serviceName,
            displayName,
            exists,
            status,
            startMode,
            pathName,
            binaryExists,
            processId,
            hasCloudConnection,
            localPortsListening ?? [5050]);
    }
}
