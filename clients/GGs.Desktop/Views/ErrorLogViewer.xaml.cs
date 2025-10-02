using System;
using System.Threading.Tasks;
using System.Windows;
using GGs.Desktop.ViewModels.ErrorLogViewer;

namespace GGs.Desktop.Views;

public partial class ErrorLogViewer : Window
{
    private readonly ErrorLogViewerViewModel _viewModel;
    private bool _initialized;

    public ErrorLogViewer()
    {
        InitializeComponent();
        _viewModel = new ErrorLogViewerViewModel();
        DataContext = _viewModel;

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized)
        {
            return;
        }

        try
        {
            await _viewModel.InitializeCommand.ExecuteAsync(null);
            _initialized = true;
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Failed to initialize ErrorLogViewer", ex);
            _viewModel.StatusText = $"Initialization failed: {ex.Message}";
        }
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        try
        {
            await _viewModel.DisposeAsync();
        }
        catch (Exception ex)
        {
            AppLogger.LogWarn($"Error disposing ErrorLogViewer resources: {ex.Message}");
        }
    }
}
