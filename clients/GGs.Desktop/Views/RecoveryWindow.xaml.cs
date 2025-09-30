using System;
using System.Windows;
using GGs.Desktop.Services;

namespace GGs.Desktop.Views
{
    public partial class RecoveryWindow : Window
    {
        public RecoveryWindow(string? lastError)
        {
            InitializeComponent();
            try { IconService.ApplyWindowIcon(this); } catch { }
            TxtError.Text = string.IsNullOrWhiteSpace(lastError)
                ? "No error details provided. Please check the Error Log Viewer for more information."
                : lastError;

            BtnRetryActivation.Click += (_, __) => Retry(() => new ModernActivationWindow(), "ModernActivationWindow");
            BtnRetryMain.Click +=       (_, __) => Retry(() => new ModernMainWindow(), "ModernMainWindow");
            BtnClose.Click += (_, __) => this.Close();
        }

        private void Retry(Func<Window> factory, string name)
        {
            try
            {
                AppLogger.LogInfo($"Recovery: trying to open {name}...");
                var win = factory();
                win.Show();
                AppLogger.LogSuccess($"Recovery: {name} opened ✅");
                this.Close();
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"Recovery: failed to open {name}", ex);
                TxtError.Text = $"❌ Failed to open {name}: {ex.Message}. We kept running. Check desktop.log for details.";
            }
        }
    }
}

