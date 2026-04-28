using Posiflora.Recovery.App.Services;
using Posiflora.Recovery.App.ViewModels;
using Wpf.Ui.Controls;

namespace Posiflora.Recovery.App;

public partial class MainWindow : FluentWindow
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel();
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await _viewModel.LoadAsync(new LocalDiagnosticsClient());
    }
}
