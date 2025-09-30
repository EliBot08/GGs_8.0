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
            
            // Set DataContext to MainViewModel from DI container
            DataContext = ((App)Application.Current).ServiceProvider.GetRequiredService<MainViewModel>();
            
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

        private void OnClosing(object sender, CancelEventArgs e)
        {
            try
            {
                // Stop monitoring when closing
                if (DataContext is MainViewModel viewModel && viewModel.IsMonitoring)
                {
                    viewModel.StopMonitoringCommand.Execute(null);
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