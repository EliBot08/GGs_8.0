using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using GGs.Desktop.Services;
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

        _viewModel.AutoScrollRequested += OnAutoScrollRequested;

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

    private void OnAutoScrollRequested(object? sender, EventArgs e)
    {
        // Scroll to the bottom of the DataGrid
        if (LogDataGrid.Items.Count > 0)
        {
            LogDataGrid.ScrollIntoView(LogDataGrid.Items[^1]);
        }
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.AutoScrollRequested -= OnAutoScrollRequested;

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
