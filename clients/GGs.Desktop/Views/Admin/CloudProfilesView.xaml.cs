using System;
using System.Windows;
using System.Windows.Controls;
using GGs.Desktop.Services;
using GGs.Desktop.ViewModels.Admin;

namespace GGs.Desktop.Views.Admin
{
    /// <summary>
    /// Enterprise Cloud Profiles Marketplace with digital signature trust and verification
    /// </summary>
    public partial class CloudProfilesView : UserControl
    {
        private readonly CloudProfilesViewModel _viewModel;

        public CloudProfilesView()
        {
            InitializeComponent();
            _viewModel = new CloudProfilesViewModel();
            DataContext = _viewModel;
            InitializeEventHandlers();
        }

        private void InitializeEventHandlers()
        {
            try
            {
                TxtSearch.TextChanged += (s, e) => _viewModel.SearchQuery = TxtSearch.Text;
                BtnRefresh.Click += async (s, e) => await _viewModel.RefreshProfilesAsync();
                BtnUploadProfile.Click += OnUploadProfileClicked;
                BtnTrustedPublishers.Click += OnTrustedPublishersClicked;
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Failed to initialize Cloud Profiles view", ex);
            }
        }

        private async void OnUploadProfileClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Select Cloud Profile to Upload",
                    Filter = "Profile Files (*.json;*.zip)|*.json;*.zip",
                    CheckFileExists = true
                };

                if (dialog.ShowDialog() == true)
                {
                    var result = await _viewModel.UploadProfileAsync(dialog.FileName);
                    MessageBox.Show(result.Success ? "Profile uploaded successfully" : $"Upload failed: {result.Message}");
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Failed to upload cloud profile", ex);
                MessageBox.Show("Failed to upload profile", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnTrustedPublishersClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                var window = new TrustedPublishersWindow { Owner = Window.GetWindow(this) };
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Failed to open trusted publishers window", ex);
            }
        }
    }
}