using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Serilog;
using USS.Desktop.App.ViewModels;
using CancelEventArgs = System.ComponentModel.CancelEventArgs;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace USS.Desktop.App;

public partial class MainWindow : Window
{
    private const double LogBottomTolerance = 4d;

    private readonly MainViewModel _viewModel;
    private bool _closeApproved;
    private bool _closeRequestInProgress;
    private bool _followLog = true;
    private bool _initialized;
    private ScrollViewer? _sessionLogScrollViewer;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoaded;
        Closing += OnClosing;
        SessionLogTextBox.TextChanged += OnSessionLogTextChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        await _viewModel.InitializeAsync();
        AttachLogScrollViewer();
        ScrollLogToEnd();
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_closeApproved)
        {
            return;
        }

        if (_closeRequestInProgress)
        {
            e.Cancel = true;
            return;
        }

        e.Cancel = true;
        _closeRequestInProgress = true;

        try
        {
            if (!await _viewModel.RequestCloseAsync())
            {
                Log.Information("Application close request cancelled.");
                return;
            }

            _closeApproved = true;
            Log.Information("Application close approved. Scheduling final window close.");
            _ = Dispatcher.BeginInvoke(Close, DispatcherPriority.Background);
        }
        finally
        {
            _closeRequestInProgress = false;
        }
    }

    private void OnSessionLogTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_followLog)
        {
            UpdateFollowLogButtonVisibility();
            return;
        }

        Dispatcher.BeginInvoke(ScrollLogToEnd, DispatcherPriority.Background);
    }

    private void OnFollowLogButtonClick(object sender, RoutedEventArgs e)
    {
        ScrollLogToEnd();
    }

    private void OnSelectorComboPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not WpfComboBox comboBox || comboBox.IsDropDownOpen || !comboBox.IsEnabled)
        {
            return;
        }

        comboBox.Focus();
        comboBox.IsDropDownOpen = true;

        if (comboBox.IsEditable && FindDescendant<WpfTextBox>(comboBox) is { } editableTextBox)
        {
            editableTextBox.Focus();
            editableTextBox.SelectAll();
        }

        e.Handled = true;
    }

    private void AttachLogScrollViewer()
    {
        if (_sessionLogScrollViewer is not null)
        {
            return;
        }

        _sessionLogScrollViewer = FindDescendant<ScrollViewer>(SessionLogTextBox);
        if (_sessionLogScrollViewer is null)
        {
            return;
        }

        _sessionLogScrollViewer.ScrollChanged += OnLogScrollChanged;
        UpdateFollowLogButtonVisibility();
    }

    private void OnLogScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_sessionLogScrollViewer is null)
        {
            return;
        }

        if (e.ExtentHeightChange > 0)
        {
            if (_followLog)
            {
                _sessionLogScrollViewer.ScrollToEnd();
            }
        }
        else
        {
            _followLog = IsLogAtBottom();
        }

        UpdateFollowLogButtonVisibility();
    }

    private void ScrollLogToEnd()
    {
        AttachLogScrollViewer();
        if (_sessionLogScrollViewer is null)
        {
            return;
        }

        _sessionLogScrollViewer.ScrollToEnd();
        _followLog = true;
        UpdateFollowLogButtonVisibility();
    }

    private bool IsLogAtBottom()
    {
        if (_sessionLogScrollViewer is null)
        {
            return true;
        }

        return _sessionLogScrollViewer.VerticalOffset >= _sessionLogScrollViewer.ScrollableHeight - LogBottomTolerance;
    }

    private void UpdateFollowLogButtonVisibility()
    {
        FollowLogButton.Visibility =
            !_followLog && !string.IsNullOrWhiteSpace(SessionLogTextBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T typedChild)
            {
                return typedChild;
            }

            var nestedChild = FindDescendant<T>(child);
            if (nestedChild is not null)
            {
                return nestedChild;
            }
        }

        return null;
    }
}
