using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using GGs.Desktop.Views;

namespace GGs.Desktop.ViewModels
{
    public class DashboardViewModel : BaseViewModel
    {
        public ICommand CreateTweakCommand { get; }
        public ICommand ManageUsersCommand { get; }
        public ICommand ViewAnalyticsCommand { get; }
        public ICommand SystemHealthCommand { get; }

        public DashboardViewModel()
        {
            CreateTweakCommand = new RelayCommand(NavigateToOptimization);
            ManageUsersCommand = new RelayCommand(ShowUserManagement);
            ViewAnalyticsCommand = new RelayCommand(NavigateToMonitoring);
            SystemHealthCommand = new RelayCommand(RunSystemHealth);
        }

        private void NavigateToOptimization()
        {
            TryNavigate("optimization");
        }

        private void ShowUserManagement()
        {
            // Navigate to Profiles (closest section hosting user/account management in this app)
            if (!TryNavigate("profiles"))
            {
                try { MessageBox.Show("User Management is available in the Profiles section.", "GGs", MessageBoxButton.OK, MessageBoxImage.Information); } catch { }
            }
        }

        private void NavigateToMonitoring()
        {
            TryNavigate("monitoring");
        }

        private void RunSystemHealth()
        {
            // Diagnostics are surfaced under Settings in ModernMainWindow
            if (!TryNavigate("settings"))
            {
                try { MessageBox.Show("Open Diagnostics from Settings.", "GGs", MessageBoxButton.OK, MessageBoxImage.Information); } catch { }
            }
        }

        private bool TryNavigate(string tab)
        {
            try
            {
                var win = Application.Current?.Windows?.OfType<ModernMainWindow>()?.FirstOrDefault();
                if (win != null)
                {
                    win.NavigateTo(tab);
                    return true;
                }
            }
            catch { }
            return false;
        }
    }
}