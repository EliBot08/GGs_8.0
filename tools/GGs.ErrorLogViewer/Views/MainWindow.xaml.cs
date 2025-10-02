#nullable enable
using System;
using System.ComponentModel;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using GGs.ErrorLogViewer.ViewModels;

namespace GGs.ErrorLogViewer.Views
{
    public partial class MainWindow : Window
    {
        private readonly ILogger<MainWindow> _logger;
        public MainWindow()
        {
            InitializeComponent();
            
            // Get logger from DI container
            _logger = ((App)Application.Current).ServiceProvider.GetRequiredService<ILogger<MainWindow>>();
            
            // Set DataContext to EnhancedMainViewModel from DI container
            DataContext = ((App)Application.Current).ServiceProvider.GetRequiredService<EnhancedMainViewModel>();
            
            // Handle window events
            Loaded += OnLoaded;
            Closing += OnClosing;
            
            _logger.LogInformation("MainWindow initialized");
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger.LogInformation("MainWindow loaded successfully");

                // Check if this is first run and show quick actions
                CheckAndShowQuickActions();

                // Set focus to search box for better UX
                if (FindName("SearchBox") is FrameworkElement searchBox)
                {
                    searchBox.Focus();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during MainWindow load");
            }
        }

        private void CheckAndShowQuickActions()
        {
            try
            {
                var settingsPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "GGs", "ErrorLogViewer", "first_run.flag");

                if (!System.IO.File.Exists(settingsPath))
                {
                    // First run - show quick actions
                    QuickActionsBar.Visibility = Visibility.Visible;

                    // Create flag file
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(settingsPath)!);
                    System.IO.File.WriteAllText(settingsPath, DateTime.UtcNow.ToString("o"));

                    _logger.LogInformation("First run detected - showing quick actions");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check first run status");
            }
        }

        private void DismissQuickActions_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                QuickActionsBar.Visibility = Visibility.Collapsed;
                _logger.LogInformation("Quick actions dismissed by user");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to dismiss quick actions");
            }
        }

        private void OnClosing(object? sender, CancelEventArgs e)
        {
            try
            {
                // Stop monitoring and dispose ViewModel when closing
                if (DataContext is MainViewModel viewModel)
                {
                    if (viewModel.IsMonitoring)
                    {
                        viewModel.StopMonitoringCommand.Execute(null);
                    }

                    // Dispose the ViewModel to clean up resources
                    if (viewModel is IDisposable disposable)
                    {
                        disposable.Dispose();
                        _logger.LogInformation("ViewModel disposed");
                    }
                }
                
                _logger.LogInformation("MainWindow closing");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during MainWindow close");
            }
        }
    }
}