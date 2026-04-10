using System.Windows;
using USS.Desktop.App.ViewModels;
using CancelEventArgs = System.ComponentModel.CancelEventArgs;

namespace USS.Desktop.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _closeApproved;
    private bool _initialized;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        await _viewModel.InitializeAsync();
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_closeApproved)
        {
            return;
        }

        e.Cancel = true;
        if (!await _viewModel.RequestCloseAsync())
        {
            return;
        }

        _closeApproved = true;
        Close();
    }
}
