using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Posiflora.Recovery.App.Services;
using Posiflora.Recovery.App.ViewModels;
using Wpf.Ui.Controls;
using Forms = System.Windows.Forms;

namespace Posiflora.Recovery.App;

public partial class MainWindow : FluentWindow
{
    private readonly MainWindowViewModel _viewModel;
    private readonly Forms.NotifyIcon _notifyIcon;
    private bool _exitRequested;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel();
        DataContext = _viewModel;
        _notifyIcon = CreateNotifyIcon();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);

        if (WindowState == System.Windows.WindowState.Minimized)
        {
            HideToTray();
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_exitRequested)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _notifyIcon.Dispose();
        base.OnClosed(e);
    }

    private void OpenLogsButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        Directory.CreateDirectory(ApplicationLogStore.DirectoryPath);
        OpenInExplorer(File.Exists(ApplicationLogStore.CurrentLogPath)
            ? ApplicationLogStore.CurrentLogPath
            : ApplicationLogStore.DirectoryPath);
    }

    private void ExportReportButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var reportPath = ApplicationLogStore.ExportReport(_viewModel.Findings);
        OpenInExplorer(reportPath);
    }

    private void MinimizeToTrayButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        HideToTray();
    }

    private Forms.NotifyIcon CreateNotifyIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Открыть POS Radar", null, (_, _) => RestoreFromTray());
        menu.Items.Add("Открыть логи", null, (_, _) => OpenLogsButton_Click(this, new System.Windows.RoutedEventArgs()));
        menu.Items.Add("Выход", null, (_, _) => ExitApplication());

        var notifyIcon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "POS Radar",
            ContextMenuStrip = menu,
            Visible = true
        };

        notifyIcon.DoubleClick += (_, _) => RestoreFromTray();
        return notifyIcon;
    }

    private void HideToTray()
    {
        ShowInTaskbar = false;
        Hide();
        _notifyIcon.Visible = true;
    }

    private void RestoreFromTray()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = System.Windows.WindowState.Normal;
        Activate();
    }

    private void ExitApplication()
    {
        _exitRequested = true;
        Close();
    }

    private static void OpenInExplorer(string path)
    {
        var argument = File.Exists(path) ? $"/select,\"{path}\"" : $"\"{path}\"";
        Process.Start(new ProcessStartInfo("explorer.exe", argument)
        {
            UseShellExecute = true
        });
    }
}
