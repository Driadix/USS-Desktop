using System.Windows;
using USS.Desktop.App.ViewModels;

namespace USS.Desktop.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _initialized;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoaded;
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
}
