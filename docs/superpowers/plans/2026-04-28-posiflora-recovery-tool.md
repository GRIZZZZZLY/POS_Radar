# Posiflora Recovery Tool Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first working vertical slice of POS Radar: a .NET 10 Windows-native diagnostic tool that detects the UEMA/ATOL service failure states already found on the current machine and shows them in a WPF + Wpf.Ui UI.

**Architecture:** Create a new .NET solution beside the legacy Python prototype. Keep diagnostic rules in `Posiflora.Recovery.Core`, Windows calls behind interfaces/adapters in `Posiflora.Recovery.Windows`, a service host in `Posiflora.Recovery.Agent`, and a WPF + Wpf.Ui operator shell in `Posiflora.Recovery.App`. The first slice uses a direct in-process fake/local data path for UI smoke testing; named-pipe IPC is planned after the UEMA profile is stable.

**Tech Stack:** .NET 10 SDK, C# 14, WPF, Wpf.Ui (`WPF-UI` NuGet package), Worker Service, xUnit, FluentAssertions, Microsoft.Extensions.Hosting, System.ServiceProcess.ServiceController.

---

## Prerequisites

The local machine currently does not expose `dotnet` in PATH. Install the .NET 10 SDK before executing implementation tasks.

Verification command:

```powershell
dotnet --version
```

Expected:

```text
10.x.x
```

If the command is missing, install the SDK and restart the terminal before continuing.

## File Structure

Create this structure under `D:\PROJECTS\Posiflora_monitoring`:

```text
POS_Radar.sln
src/
  Posiflora.Recovery.Core/
  Posiflora.Recovery.Windows/
  Posiflora.Recovery.Agent/
  Posiflora.Recovery.App/
tests/
  Posiflora.Recovery.Core.Tests/
```

Legacy files remain:

```text
uem_monitor.py
PosifloraUemMonitor.spec
```

Generated artifacts are ignored:

```text
build/
dist/
.teamly-browser-profile/
teamly-*.png
```

## Task 1: Scaffold Solution

**Files:**
- Create: `POS_Radar.sln`
- Create: `src/Posiflora.Recovery.Core/Posiflora.Recovery.Core.csproj`
- Create: `src/Posiflora.Recovery.Windows/Posiflora.Recovery.Windows.csproj`
- Create: `src/Posiflora.Recovery.Agent/Posiflora.Recovery.Agent.csproj`
- Create: `src/Posiflora.Recovery.App/Posiflora.Recovery.App.csproj`
- Create: `tests/Posiflora.Recovery.Core.Tests/Posiflora.Recovery.Core.Tests.csproj`

- [ ] **Step 1: Create the solution and projects**

```powershell
dotnet new sln -n POS_Radar
dotnet new classlib -n Posiflora.Recovery.Core -o src/Posiflora.Recovery.Core
dotnet new classlib -n Posiflora.Recovery.Windows -o src/Posiflora.Recovery.Windows
dotnet new worker -n Posiflora.Recovery.Agent -o src/Posiflora.Recovery.Agent
dotnet new wpf -n Posiflora.Recovery.App -o src/Posiflora.Recovery.App
dotnet new xunit -n Posiflora.Recovery.Core.Tests -o tests/Posiflora.Recovery.Core.Tests
dotnet sln POS_Radar.sln add src/Posiflora.Recovery.Core/Posiflora.Recovery.Core.csproj
dotnet sln POS_Radar.sln add src/Posiflora.Recovery.Windows/Posiflora.Recovery.Windows.csproj
dotnet sln POS_Radar.sln add src/Posiflora.Recovery.Agent/Posiflora.Recovery.Agent.csproj
dotnet sln POS_Radar.sln add src/Posiflora.Recovery.App/Posiflora.Recovery.App.csproj
dotnet sln POS_Radar.sln add tests/Posiflora.Recovery.Core.Tests/Posiflora.Recovery.Core.Tests.csproj
```

Expected: project creation succeeds and all projects are added to the solution.

- [ ] **Step 2: Add references and packages**

```powershell
dotnet add src/Posiflora.Recovery.Windows/Posiflora.Recovery.Windows.csproj reference src/Posiflora.Recovery.Core/Posiflora.Recovery.Core.csproj
dotnet add src/Posiflora.Recovery.Agent/Posiflora.Recovery.Agent.csproj reference src/Posiflora.Recovery.Core/Posiflora.Recovery.Core.csproj
dotnet add src/Posiflora.Recovery.Agent/Posiflora.Recovery.Agent.csproj reference src/Posiflora.Recovery.Windows/Posiflora.Recovery.Windows.csproj
dotnet add src/Posiflora.Recovery.App/Posiflora.Recovery.App.csproj reference src/Posiflora.Recovery.Core/Posiflora.Recovery.Core.csproj
dotnet add tests/Posiflora.Recovery.Core.Tests/Posiflora.Recovery.Core.Tests.csproj reference src/Posiflora.Recovery.Core/Posiflora.Recovery.Core.csproj
dotnet add src/Posiflora.Recovery.App/Posiflora.Recovery.App.csproj package WPF-UI --version 4.2.0
dotnet add tests/Posiflora.Recovery.Core.Tests/Posiflora.Recovery.Core.Tests.csproj package FluentAssertions
```

- [ ] **Step 3: Build empty solution**

```powershell
dotnet build POS_Radar.sln
```

Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```powershell
git add POS_Radar.sln src tests
git commit -m "chore: scaffold dotnet recovery solution"
```

## Task 2: Core Diagnostic Models

**Files:**
- Create: `src/Posiflora.Recovery.Core/Diagnostics/FindingSeverity.cs`
- Create: `src/Posiflora.Recovery.Core/Diagnostics/RemediationAction.cs`
- Create: `src/Posiflora.Recovery.Core/Diagnostics/Finding.cs`
- Create: `src/Posiflora.Recovery.Core/Diagnostics/CheckResult.cs`
- Create: `src/Posiflora.Recovery.Core/Diagnostics/DiagnosticProfile.cs`
- Test: `tests/Posiflora.Recovery.Core.Tests/UemaFindingMapperTests.cs`

- [ ] **Step 1: Write failing model usage test**

Create `tests/Posiflora.Recovery.Core.Tests/UemaFindingMapperTests.cs`:

```csharp
using FluentAssertions;
using Posiflora.Recovery.Core.Diagnostics;
using Xunit;

namespace Posiflora.Recovery.Core.Tests;

public sealed class UemaFindingMapperTests
{
    [Fact]
    public void Finding_exposes_stable_operator_fields()
    {
        var finding = new Finding(
            Id: "uem.service.missing_binary",
            Severity: FindingSeverity.Critical,
            Title: "UEM Agent binary is missing",
            Evidence: "Path does not exist: C:\\Program Files\\UEM\\Agent\\bin\\uema.exe",
            Explanation: "The Windows service exists, but its executable is missing.",
            RecommendedAction: "Reinstall or repair the ATOL KKT driver package.",
            CanAutoFix: false,
            Actions: Array.Empty<RemediationAction>(),
            Source: "Teamly: Решение проблем / UEMA (11)");

        finding.Id.Should().Be("uem.service.missing_binary");
        finding.Severity.Should().Be(FindingSeverity.Critical);
        finding.CanAutoFix.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```powershell
dotnet test tests/Posiflora.Recovery.Core.Tests/Posiflora.Recovery.Core.Tests.csproj --filter Finding_exposes_stable_operator_fields
```

Expected: compile failure because `Finding` and related types do not exist.

- [ ] **Step 3: Add diagnostic model types**

Create `src/Posiflora.Recovery.Core/Diagnostics/FindingSeverity.cs`:

```csharp
namespace Posiflora.Recovery.Core.Diagnostics;

public enum FindingSeverity
{
    Info = 0,
    Warning = 1,
    Critical = 2
}
```

Create `src/Posiflora.Recovery.Core/Diagnostics/RemediationAction.cs`:

```csharp
namespace Posiflora.Recovery.Core.Diagnostics;

public sealed record RemediationAction(
    string Id,
    string Title,
    string Description,
    bool RequiresConfirmation);
```

Create `src/Posiflora.Recovery.Core/Diagnostics/Finding.cs`:

```csharp
namespace Posiflora.Recovery.Core.Diagnostics;

public sealed record Finding(
    string Id,
    FindingSeverity Severity,
    string Title,
    string Evidence,
    string Explanation,
    string RecommendedAction,
    bool CanAutoFix,
    IReadOnlyList<RemediationAction> Actions,
    string Source);
```

Create `src/Posiflora.Recovery.Core/Diagnostics/CheckResult.cs`:

```csharp
namespace Posiflora.Recovery.Core.Diagnostics;

public sealed record CheckResult(
    string CheckId,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    IReadOnlyList<Finding> Findings)
{
    public bool HasCriticalFindings => Findings.Any(f => f.Severity == FindingSeverity.Critical);
}
```

Create `src/Posiflora.Recovery.Core/Diagnostics/DiagnosticProfile.cs`:

```csharp
namespace Posiflora.Recovery.Core.Diagnostics;

public sealed record DiagnosticProfile(
    string Id,
    string Title,
    string Description);
```

- [ ] **Step 4: Run test to verify it passes**

```powershell
dotnet test tests/Posiflora.Recovery.Core.Tests/Posiflora.Recovery.Core.Tests.csproj --filter Finding_exposes_stable_operator_fields
```

Expected: `Passed!`

- [ ] **Step 5: Commit**

```powershell
git add src/Posiflora.Recovery.Core tests/Posiflora.Recovery.Core.Tests
git commit -m "feat: add diagnostic finding models"
```

## Task 3: UEMA Snapshot and Finding Mapper

**Files:**
- Create: `src/Posiflora.Recovery.Core/Uema/UemaSnapshot.cs`
- Create: `src/Posiflora.Recovery.Core/Uema/UemaFindingMapper.cs`
- Test: `tests/Posiflora.Recovery.Core.Tests/UemaFindingMapperTests.cs`

- [ ] **Step 1: Replace tests with UEMA fixture tests**

Replace `tests/Posiflora.Recovery.Core.Tests/UemaFindingMapperTests.cs`:

```csharp
using FluentAssertions;
using Posiflora.Recovery.Core.Diagnostics;
using Posiflora.Recovery.Core.Uema;
using Xunit;

namespace Posiflora.Recovery.Core.Tests;

public sealed class UemaFindingMapperTests
{
    [Fact]
    public void Maps_missing_binary_as_critical_not_auto_fixable()
    {
        var snapshot = new UemaSnapshot(
            ServiceName: "uem-agent",
            DisplayName: "UEM: Agent",
            Exists: true,
            Status: "Stopped",
            StartMode: "Auto",
            PathName: @"C:\Program Files\UEM\Agent\bin\uema.exe",
            BinaryExists: false,
            ProcessId: 0,
            HasCloudConnection: false,
            LocalPortsListening: Array.Empty<int>());

        var findings = UemaFindingMapper.Map(snapshot);

        findings.Should().ContainSingle(f => f.Id == "uem.service.missing_binary");
        var finding = findings.Single(f => f.Id == "uem.service.missing_binary");
        finding.Severity.Should().Be(FindingSeverity.Critical);
        finding.CanAutoFix.Should().BeFalse();
        finding.RecommendedAction.Should().Contain("driver");
    }

    [Fact]
    public void Maps_stopped_service_with_existing_binary_as_repairable()
    {
        var snapshot = new UemaSnapshot(
            ServiceName: "uem-agent",
            DisplayName: "UEM: Agent",
            Exists: true,
            Status: "Stopped",
            StartMode: "Auto",
            PathName: @"C:\Program Files\UEM\Agent\bin\uema.exe",
            BinaryExists: true,
            ProcessId: 0,
            HasCloudConnection: false,
            LocalPortsListening: Array.Empty<int>());

        var findings = UemaFindingMapper.Map(snapshot);

        findings.Should().ContainSingle(f => f.Id == "uem.service.stopped");
        var finding = findings.Single(f => f.Id == "uem.service.stopped");
        finding.Severity.Should().Be(FindingSeverity.Warning);
        finding.CanAutoFix.Should().BeTrue();
        finding.Actions.Should().Contain(a => a.Id == "service.start.uem-agent");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```powershell
dotnet test tests/Posiflora.Recovery.Core.Tests/Posiflora.Recovery.Core.Tests.csproj --filter UemaFindingMapperTests
```

Expected: compile failure because `UemaSnapshot` and `UemaFindingMapper` do not exist.

- [ ] **Step 3: Implement snapshot and mapper**

Create `src/Posiflora.Recovery.Core/Uema/UemaSnapshot.cs`:

```csharp
namespace Posiflora.Recovery.Core.Uema;

public sealed record UemaSnapshot(
    string ServiceName,
    string DisplayName,
    bool Exists,
    string Status,
    string StartMode,
    string PathName,
    bool BinaryExists,
    int ProcessId,
    bool HasCloudConnection,
    IReadOnlyList<int> LocalPortsListening);
```

Create `src/Posiflora.Recovery.Core/Uema/UemaFindingMapper.cs`:

```csharp
using Posiflora.Recovery.Core.Diagnostics;

namespace Posiflora.Recovery.Core.Uema;

public static class UemaFindingMapper
{
    public static IReadOnlyList<Finding> Map(UemaSnapshot snapshot)
    {
        var findings = new List<Finding>();

        if (!snapshot.Exists)
        {
            findings.Add(new Finding(
                "uem.service.missing",
                FindingSeverity.Critical,
                $"{snapshot.DisplayName} service is missing",
                $"Service name was not found: {snapshot.ServiceName}",
                "The ATOL/UEM service is not registered in Windows Service Control Manager.",
                "Reinstall or repair the ATOL KKT driver package.",
                false,
                Array.Empty<RemediationAction>(),
                "Teamly: Решение проблем / UEMA (11)"));
            return findings;
        }

        if (!snapshot.BinaryExists)
        {
            findings.Add(new Finding(
                "uem.service.missing_binary",
                FindingSeverity.Critical,
                $"{snapshot.DisplayName} binary is missing",
                $"Service exists, but PathName does not exist: {snapshot.PathName}",
                "Windows has a registered UEM service, but the executable it points to is missing. This usually means the ATOL driver package is partially removed or damaged.",
                "Repair or reinstall the ATOL KKT driver package. Do not only restart the service: Windows has no executable to start.",
                false,
                Array.Empty<RemediationAction>(),
                "Teamly: Решение проблем / UEMA (11)"));
            return findings;
        }

        if (!string.Equals(snapshot.Status, "Running", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(new Finding(
                "uem.service.stopped",
                FindingSeverity.Warning,
                $"{snapshot.DisplayName} is not running",
                $"Status={snapshot.Status}; StartMode={snapshot.StartMode}; PathName={snapshot.PathName}",
                "The UEM service is installed and its executable exists, but the service is not running.",
                "Start the service and re-run diagnostics.",
                true,
                new[]
                {
                    new RemediationAction(
                        $"service.start.{snapshot.ServiceName}",
                        "Start service",
                        $"Start Windows service {snapshot.ServiceName}.",
                        true)
                },
                "Built-in rule: UEM service status"));
        }

        return findings;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```powershell
dotnet test tests/Posiflora.Recovery.Core.Tests/Posiflora.Recovery.Core.Tests.csproj --filter UemaFindingMapperTests
```

Expected: `Passed!`

- [ ] **Step 5: Commit**

```powershell
git add src/Posiflora.Recovery.Core/Uema tests/Posiflora.Recovery.Core.Tests/UemaFindingMapperTests.cs
git commit -m "feat: map uema service states to findings"
```

## Task 4: WPF + Wpf.Ui Shell

**Files:**
- Modify: `src/Posiflora.Recovery.App/App.xaml`
- Modify: `src/Posiflora.Recovery.App/MainWindow.xaml`
- Modify: `src/Posiflora.Recovery.App/MainWindow.xaml.cs`
- Create: `src/Posiflora.Recovery.App/ViewModels/MainWindowViewModel.cs`
- Create: `src/Posiflora.Recovery.App/ViewModels/FindingViewModel.cs`
- Create: `src/Posiflora.Recovery.App/Services/FakeDiagnosticsClient.cs`

- [ ] **Step 1: Create view models and fake client**

Create `src/Posiflora.Recovery.App/ViewModels/FindingViewModel.cs`:

```csharp
namespace Posiflora.Recovery.App.ViewModels;

public sealed record FindingViewModel(
    string Severity,
    string Title,
    string Evidence,
    string RecommendedAction);
```

Create `src/Posiflora.Recovery.App/Services/FakeDiagnosticsClient.cs`:

```csharp
using Posiflora.Recovery.App.ViewModels;

namespace Posiflora.Recovery.App.Services;

public sealed class FakeDiagnosticsClient
{
    public IReadOnlyList<FindingViewModel> GetFindings()
    {
        return new[]
        {
            new FindingViewModel(
                "Critical",
                "UEM Agent binary is missing",
                @"Service exists, but PathName does not exist: C:\Program Files\UEM\Agent\bin\uema.exe",
                "Repair or reinstall the ATOL KKT driver package."),
            new FindingViewModel(
                "Critical",
                "UEM Updater binary is missing",
                @"Service exists, but PathName does not exist: C:\Program Files\UEM\Updater\bin\uemu.exe",
                "Repair or reinstall the ATOL KKT driver package.")
        };
    }
}
```

Create `src/Posiflora.Recovery.App/ViewModels/MainWindowViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using Posiflora.Recovery.App.Services;

namespace Posiflora.Recovery.App.ViewModels;

public sealed class MainWindowViewModel
{
    public string AgentStatus { get; } = "Agent: Demo";
    public string OverallStatus { get; } = "Critical";
    public string LastCheck { get; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    public ObservableCollection<FindingViewModel> Findings { get; }

    public MainWindowViewModel()
    {
        Findings = new ObservableCollection<FindingViewModel>(
            new FakeDiagnosticsClient().GetFindings());
    }
}
```

- [ ] **Step 2: Configure Wpf.Ui resources**

Replace `src/Posiflora.Recovery.App/App.xaml`:

```xml
<Application x:Class="Posiflora.Recovery.App.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
             StartupUri="MainWindow.xaml">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ui:ThemesDictionary Theme="Light" />
                <ui:ControlsDictionary />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

- [ ] **Step 3: Add first Wpf.Ui shell**

Replace `src/Posiflora.Recovery.App/MainWindow.xaml` with the first operator layout:

```xml
<ui:FluentWindow x:Class="Posiflora.Recovery.App.MainWindow"
                 xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                 xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
                 Title="POS Radar"
                 Width="1180"
                 Height="760"
                 MinWidth="980"
                 MinHeight="640">
    <Grid Margin="24">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <DockPanel Grid.Row="0" LastChildFill="True">
            <TextBlock Text="POS Radar" FontSize="24" FontWeight="SemiBold" />
            <TextBlock Text="{Binding AgentStatus}" HorizontalAlignment="Right" Foreground="#2E7D32" />
        </DockPanel>

        <Border Grid.Row="1" Margin="0,20,0,16" Padding="16" CornerRadius="8" Background="#F5F7FA">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*" />
                    <ColumnDefinition Width="Auto" />
                </Grid.ColumnDefinitions>
                <StackPanel>
                    <TextBlock Text="{Binding OverallStatus}" FontSize="20" FontWeight="SemiBold" Foreground="#C62828" />
                    <TextBlock Text="{Binding LastCheck, StringFormat=Last check: {0}}" Margin="0,6,0,0" Foreground="#5F6368" />
                </StackPanel>
                <StackPanel Grid.Column="1" Orientation="Horizontal">
                    <ui:Button Content="Проверить все" Margin="0,0,8,0" />
                    <ui:Button Content="Собрать отчет" Appearance="Primary" />
                </StackPanel>
            </Grid>
        </Border>

        <Grid Grid.Row="2">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="220" />
                <ColumnDefinition Width="16" />
                <ColumnDefinition Width="*" />
            </Grid.ColumnDefinitions>

            <StackPanel Grid.Column="0">
                <TextBlock Text="Профили" FontWeight="SemiBold" Margin="0,0,0,12" />
                <ui:Button Content="UEMA / ошибка 11" Appearance="Primary" HorizontalAlignment="Stretch" Margin="0,0,0,8" />
                <ui:Button Content="Сеть кассы" HorizontalAlignment="Stretch" Margin="0,0,0,8" />
                <ui:Button Content="USB / COM" HorizontalAlignment="Stretch" Margin="0,0,0,8" />
                <ui:Button Content="ОФД" HorizontalAlignment="Stretch" Margin="0,0,0,8" />
            </StackPanel>

            <ListBox Grid.Column="2" ItemsSource="{Binding Findings}">
                <ListBox.ItemTemplate>
                    <DataTemplate>
                        <StackPanel Margin="8">
                            <TextBlock Text="{Binding Title}" FontWeight="SemiBold" />
                            <TextBlock Text="{Binding Severity}" Foreground="#C62828" FontSize="12" />
                            <TextBlock Text="{Binding Evidence}" Foreground="#5F6368" TextWrapping="Wrap" />
                        </StackPanel>
                    </DataTemplate>
                </ListBox.ItemTemplate>
            </ListBox>
        </Grid>
    </Grid>
</ui:FluentWindow>
```

- [ ] **Step 4: Set DataContext**

Replace `src/Posiflora.Recovery.App/MainWindow.xaml.cs`:

```csharp
using Posiflora.Recovery.App.ViewModels;
using Wpf.Ui.Controls;

namespace Posiflora.Recovery.App;

public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}
```

- [ ] **Step 5: Run UI smoke**

```powershell
dotnet run --project src/Posiflora.Recovery.App/Posiflora.Recovery.App.csproj
```

Expected: Wpf.Ui window opens with profile buttons and two critical UEMA findings.

- [ ] **Step 6: Commit**

```powershell
git add src/Posiflora.Recovery.App
git commit -m "feat: add wpf ui shell"
```

## Task 5: Agent Skeleton

**Files:**
- Modify: `src/Posiflora.Recovery.Agent/Program.cs`
- Modify: `src/Posiflora.Recovery.Agent/Worker.cs`

- [ ] **Step 1: Add minimal worker**

Replace `src/Posiflora.Recovery.Agent/Program.cs`:

```csharp
using Posiflora.Recovery.Agent;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
var host = builder.Build();
host.Run();
```

Replace `src/Posiflora.Recovery.Agent/Worker.cs`:

```csharp
namespace Posiflora.Recovery.Agent;

public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("POS Radar agent started at {Time}", DateTimeOffset.Now);

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("POS Radar agent heartbeat at {Time}", DateTimeOffset.Now);
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
```

- [ ] **Step 2: Run agent smoke**

```powershell
dotnet run --project src/Posiflora.Recovery.Agent/Posiflora.Recovery.Agent.csproj
```

Expected: agent starts and logs heartbeat. Stop with `Ctrl+C`.

- [ ] **Step 3: Commit**

```powershell
git add src/Posiflora.Recovery.Agent
git commit -m "feat: add recovery agent skeleton"
```

## Task 6: Verification Pass

**Files:**
- Read: `docs/superpowers/specs/2026-04-27-posiflora-recovery-tool-design.md`
- Read: all files created in `src/` and `tests/`

- [ ] **Step 1: Run full test suite**

```powershell
dotnet test POS_Radar.sln
```

Expected: `Passed!`

- [ ] **Step 2: Run full build**

```powershell
dotnet build POS_Radar.sln
```

Expected: `Build succeeded.`

- [ ] **Step 3: Run UI smoke**

```powershell
dotnet run --project src/Posiflora.Recovery.App/Posiflora.Recovery.App.csproj
```

Expected: Wpf.Ui shell opens and demo UEMA findings are visible.

- [ ] **Step 4: Run agent smoke**

```powershell
dotnet run --project src/Posiflora.Recovery.Agent/Posiflora.Recovery.Agent.csproj
```

Expected: agent starts, logs heartbeat, and exits cleanly on `Ctrl+C`.

## Deferred Work

These are intentionally outside this first implementation plan:

- named-pipe IPC between UI and Agent;
- installing Agent as a Windows Service;
- real firewall rule creation;
- real service start/restart actions;
- ZIP report generation;
- live TCP connection adapter;
- USB/COM adapter;
- OFD profile;
- cash register network profile;
- installer.

They should each get separate follow-up tasks after the first UEMA vertical slice builds and runs.

## Plan Self-Review

Spec coverage:
- WPF + Wpf.Ui UI shell is covered by Task 4.
- UEMA/error 11 diagnostic logic is covered by Tasks 2 and 3.
- Agent skeleton is covered by Task 5.
- Verification is covered by Task 6.

Known intentional gap:
- Full IPC and service installation are deferred because the first slice needs a stable diagnostic model and UI before introducing privileged process boundaries.

