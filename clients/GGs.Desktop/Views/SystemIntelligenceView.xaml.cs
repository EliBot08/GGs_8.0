using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using GGs.Shared.SystemIntelligence;
// using GGs.Shared.SystemIntelligence.Models; // removed - namespace not present
// using GGs.Shared.SystemIntelligence.Enums; // keep commented to avoid ambiguity
using GGs.Shared.Enums;
using GGs.Desktop.ViewModels;
using SharedIntelligenceService = GGs.Shared.SystemIntelligence.SystemIntelligenceService;

namespace GGs.Desktop.Views
{
    /// <summary>
    /// Premium System Intelligence Harvester UI with enterprise-grade functionality
    /// Provides real-time progress tracking and comprehensive system analysis
    /// </summary>
    public partial class SystemIntelligenceView : UserControl, INotifyPropertyChanged
    {
        private readonly ILogger<SystemIntelligenceView>? _logger;
        private readonly SharedIntelligenceService _systemIntelligenceService;
        private readonly DispatcherTimer _progressTimer;
        private readonly DispatcherTimer _elapsedTimer;
        
        private CancellationTokenSource? _scanCancellationTokenSource;
        private DateTime _scanStartTime;
        private SystemIntelligenceProfile? _currentProfile;
        private bool _isScanInProgress;

        public ObservableCollection<DetectedTweakViewModel> DetectedTweaks { get; set; }
        public ObservableCollection<ProfileSummaryViewModel> RecentProfiles { get; set; }
        public event PropertyChangedEventHandler? PropertyChanged;

        public SystemIntelligenceView()
        {
            InitializeComponent();
            
            // Initialize services
            try
            {
                var serviceProvider = App.ServiceProvider;
                _logger = serviceProvider?.GetService<ILogger<SystemIntelligenceView>>();
            }
            catch
            {
                _logger = null;
            }
            
            // Initialize shared SystemIntelligenceService directly (no DI available here)
            _systemIntelligenceService = new SharedIntelligenceService();
            
            // Subscribe to shared service events
            _systemIntelligenceService.ScanProgressChanged += OnScanProgressChanged;
            _systemIntelligenceService.TweakDetected += OnTweakDetected;
            _systemIntelligenceService.ScanCompleted += OnScanCompleted;
            
            // Initialize collections
            DetectedTweaks = new ObservableCollection<DetectedTweakViewModel>();
            RecentProfiles = new ObservableCollection<ProfileSummaryViewModel>();
            
            // Set data context
            DataContext = this;
            DetectedTweaksList.ItemsSource = DetectedTweaks;
            RecentProfilesList.ItemsSource = RecentProfiles;

            // Initialize timers
            _progressTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _progressTimer.Tick += ProgressTimer_Tick;

            _elapsedTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _elapsedTimer.Tick += ElapsedTimer_Tick;

            // Services are now initialized in constructor
            
            // Load initial data
            LoadRecentProfiles();
            UpdateLicenseInformation();
        }


        private async void StartHarvest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_isScanInProgress)
                {
                    await StopScanAsync();
                    return;
                }

                // Validate license
                if (!await ValidateLicenseAsync())
                {
                    ShowLicenseUpgradeDialog();
                    return;
                }

                // Get scan configuration
                var scanConfig = GetScanConfiguration();
                
                // Validate privileges
                if (!await ValidatePrivilegesAsync())
                {
                    var result = MessageBox.Show(
                        "System Intelligence Harvester requires administrator privileges for deep system access. " +
                        "Would you like to restart with elevated permissions?",
                        "Elevation Required",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        await RequestElevationAsync();
                    }
                    return;
                }

                await StartScanAsync(scanConfig);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to start system intelligence harvest");
                ShowErrorMessage("Failed to start harvest", ex.Message);
            }
        }

        private async Task StartScanAsync(ScanConfiguration config)
        {
            try
            {
                _isScanInProgress = true;
                _scanStartTime = DateTime.Now;
                _scanCancellationTokenSource = new CancellationTokenSource();

                // Update UI
                UpdateUIForScanStart();

                // Clear previous results
                DetectedTweaks.Clear();
                ResetProgressBars();

                // Build DeepHarvestOptions from UI configuration
                var areas = ScanArea.None;
                if (config.IncludeRegistry) areas |= ScanArea.Registry;
                if (config.IncludeServices) areas |= ScanArea.Services;
                if (config.IncludeBios) areas |= ScanArea.BiosUefi;
                if (config.IncludeGroupPolicy) areas |= ScanArea.GroupPolicy;
                if (config.IncludeThirdParty) areas |= ScanArea.ThirdPartyApps;

                var options = new DeepHarvestOptions
                {
                    ScanDepth = (int)config.ScanDepth,
                    ScanAreas = areas,
                    ProfileName = $"Local Scan {DateTime.Now:yyyy-MM-dd HH:mm} ",
                    IncludeSystemInfo = true,
                    AnalyzeTweaks = true,
                    CreateBackup = true
                };

                // Resolve user id and license tier
                var payload = new GGs.Desktop.Services.LicenseService().CurrentPayload;
                var userId = payload?.UserId ?? string.Empty;
                var userTier = (GGs.Shared.SystemIntelligence.LicenseTier)(int)(payload?.Tier ?? GGs.Shared.Enums.LicenseTier.Basic);

                // Start timers before launching scan
                _progressTimer.Start();
                _elapsedTimer.Start();

                // Fire-and-forget start; results will arrive via events
                _ = _systemIntelligenceService.StartDeepHarvestAsync(
                    userId,
                    userTier,
                    options,
                    _scanCancellationTokenSource.Token);

                _logger?.LogInformation("System Intelligence harvest started with depth: {ScanDepth}", config.ScanDepth);
            }
            catch (Exception ex)
            {
                _isScanInProgress = false;
                _logger?.LogError(ex, "Failed to start scan");
                ShowErrorMessage("Scan Failed", ex.Message);
                UpdateUIForScanComplete();
            }
        }

        private async Task StopScanAsync()
        {
            try
            {
                _scanCancellationTokenSource?.Cancel();
                _isScanInProgress = false;
                
                _progressTimer.Stop();
                _elapsedTimer.Stop();

                UpdateUIForScanComplete();
                
                _logger?.LogInformation("System Intelligence harvest stopped by user");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to stop scan");
            }
        }

        private void OnScanProgressChanged(object? sender, GGs.Shared.SystemIntelligence.ScanProgressEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    // Map shared progress model to UI
                    var progressPercentage = e.Progress;

                    // Update overall progress
                    ScanProgressRing.Value = progressPercentage;
                    CurrentOperationText.Text = e.CurrentOperation ?? string.Empty;

                    // Update individual progress bars (approximate with overall percentage)
                    RegistryProgress.Value = e.Progress;
                    RegistryProgressText.Text = $"{e.Progress:F0}%";

                    ServiceProgress.Value = e.Progress;
                    ServiceProgressText.Text = $"{e.Progress:F0}%";

                    BiosProgress.Value = e.Progress;
                    BiosProgressText.Text = $"{e.Progress:F0}%";

                    GroupPolicyProgress.Value = e.Progress;
                    GroupPolicyProgressText.Text = $"{e.Progress:F0}%";

                    // Update estimated time
                    EstimatedTimeText.Text = e.Status ?? "Unknown";
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to update scan progress");
                }
            });
        }

        private void OnTweakDetected(object? sender, GGs.Shared.SystemIntelligence.TweakDetectedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    var tweakViewModel = new DetectedTweakViewModel
                    {
                        Name = e.Tweak.Name,
                        Description = e.Tweak.Description,
                        Source = e.Tweak.Source.ToString(),
                        Category = e.Tweak.Category.ToString(),
                        ConfidenceScore = (int)(e.Tweak.ConfidenceScore * 100),
                        SafetyColor = GetSafetyColor(GetRiskLevel(e.Tweak.EstimatedRisk)),
                        IsSelected = GetRiskLevel(e.Tweak.EstimatedRisk) <= GGs.Shared.SystemIntelligence.RiskLevel.Low
                    };

                    DetectedTweaks.Add(tweakViewModel);
                    TweaksFoundText.Text = DetectedTweaks.Count.ToString();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to add detected tweak");
                }
            });
        }

        private void OnScanCompleted(object? sender, GGs.Shared.SystemIntelligence.ScanCompletedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    _isScanInProgress = false;
                    _progressTimer.Stop();
                    _elapsedTimer.Stop();

                    // Update UI
                    UpdateUIForScanComplete();
                    
                    // Create a profile from the detected tweaks
                    var profile = new SystemIntelligenceProfile
                    {
                        Tweaks = e.DetectedTweaks,
                        CreatedAt = DateTime.UtcNow,
                        Name = $"Scan {DateTime.Now:yyyy-MM-dd HH:mm}"
                    };
                    ShowScanResults(profile);

                    // Auto-save if enabled
                    if (AutoSaveCheckBox.IsChecked == true)
                    {
                        _ = Task.Run(() => AutoSaveProfile(profile));
                    }

                    _logger?.LogInformation("System Intelligence harvest completed. Found {TweakCount} tweaks", 
                        e.DetectedTweaks.Count);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to handle scan completion");
                }
            });
        }

        private void OnScanError(object sender, ScanErrorEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    _isScanInProgress = false;
                    _progressTimer.Stop();
                    _elapsedTimer.Stop();

                    UpdateUIForScanComplete();
                    ShowErrorMessage("Scan Error", e.ErrorMessage);

                    _logger?.LogError("System Intelligence harvest failed: {Error}", e.ErrorMessage);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to handle scan error");
                }
            });
        }

        private void UpdateUIForScanStart()
        {
            BtnStartHarvest.Content = "🛑 Stop Harvest";
            BtnStartHarvest.Background = (System.Windows.Media.Brush)FindResource("DangerGradient");
            
            WelcomeCard.Visibility = Visibility.Collapsed;
            ScanProgressCard.Visibility = Visibility.Visible;
            ScanResultsCard.Visibility = Visibility.Collapsed;

            // Disable configuration controls
            ScanDepthCombo.IsEnabled = false;
            RegistryCheckBox.IsEnabled = false;
            ServicesCheckBox.IsEnabled = false;
            BiosCheckBox.IsEnabled = false;
            GroupPolicyCheckBox.IsEnabled = false;
            ThirdPartyCheckBox.IsEnabled = false;
            SafetyLevelCombo.IsEnabled = false;
        }

        private void UpdateUIForScanComplete()
        {
            BtnStartHarvest.Content = "🚀 Start Deep Harvest";
            BtnStartHarvest.Background = (System.Windows.Media.Brush)FindResource("PremiumGradient");
            
            ScanProgressCard.Visibility = Visibility.Collapsed;
            
            // Re-enable configuration controls
            ScanDepthCombo.IsEnabled = true;
            RegistryCheckBox.IsEnabled = true;
            ServicesCheckBox.IsEnabled = true;
            BiosCheckBox.IsEnabled = true;
            GroupPolicyCheckBox.IsEnabled = true;
            ThirdPartyCheckBox.IsEnabled = true;
            SafetyLevelCombo.IsEnabled = true;
        }

        private void ShowScanResults(SystemIntelligenceProfile profile)
        {
            ScanResultsCard.Visibility = Visibility.Visible;
            WelcomeCard.Visibility = Visibility.Collapsed;

            // Update summary statistics
            var totalTweaks = profile.DetectedTweaks.Count;
            var safeTweaks = profile.DetectedTweaks.Count(t => GetRiskLevel(t.EstimatedRisk) == GGs.Shared.SystemIntelligence.RiskLevel.Unknown || GetRiskLevel(t.EstimatedRisk) == GGs.Shared.SystemIntelligence.RiskLevel.Low);
            var cautionTweaks = profile.DetectedTweaks.Count(t => GetRiskLevel(t.EstimatedRisk) == GGs.Shared.SystemIntelligence.RiskLevel.Medium);
            var riskyTweaks = profile.DetectedTweaks.Count(t => GetRiskLevel(t.EstimatedRisk) == GGs.Shared.SystemIntelligence.RiskLevel.High || GetRiskLevel(t.EstimatedRisk) == GGs.Shared.SystemIntelligence.RiskLevel.Critical);

            TotalTweaksText.Text = totalTweaks.ToString();
            SafeTweaksText.Text = safeTweaks.ToString();
            CautionTweaksText.Text = cautionTweaks.ToString();
            RiskyTweaksText.Text = riskyTweaks.ToString();

            var scanDuration = DateTime.Now - _scanStartTime;
            ScanSummaryText.Text = $"Completed in {scanDuration:mm\\:ss} - {totalTweaks} tweaks discovered";
        }

        private void ResetProgressBars()
        {
            ScanProgressRing.Value = 0;
            RegistryProgress.Value = 0;
            ServiceProgress.Value = 0;
            BiosProgress.Value = 0;
            GroupPolicyProgress.Value = 0;

            RegistryProgressText.Text = "0%";
            ServiceProgressText.Text = "0%";
            BiosProgressText.Text = "0%";
            GroupPolicyProgressText.Text = "0%";

            TweaksFoundText.Text = "0";
            ElapsedTimeText.Text = "00:00:00";
            EstimatedTimeText.Text = "--:--:--";
        }

        private void ProgressTimer_Tick(object? sender, EventArgs e)
        {
            // Update any animated progress indicators
            // This could include pulsing effects, rotating icons, etc.
        }

        private void ElapsedTimer_Tick(object? sender, EventArgs e)
        {
            if (_isScanInProgress)
            {
                var elapsed = DateTime.Now - _scanStartTime;
                ElapsedTimeText.Text = elapsed.ToString(@"hh\:mm\:ss");
            }
        }

        private ScanConfiguration GetScanConfiguration()
        {
            var selectedDepth = ((ComboBoxItem)ScanDepthCombo.SelectedItem)?.Tag?.ToString();
            Enum.TryParse<ScanDepth>(selectedDepth, out var scanDepth);

            var selectedSafety = ((ComboBoxItem)SafetyLevelCombo.SelectedItem)?.Tag?.ToString();
            var safetyLevel = MapSafetyTagToSystemSafetyLevel(selectedSafety);

            return new ScanConfiguration
            {
                ScanDepth = scanDepth,
                SafetyLevel = safetyLevel,
                IncludeRegistry = RegistryCheckBox.IsChecked == true,
                IncludeServices = ServicesCheckBox.IsChecked == true,
                IncludeBios = BiosCheckBox.IsChecked == true,
                IncludeGroupPolicy = GroupPolicyCheckBox.IsChecked == true,
                IncludeThirdParty = ThirdPartyCheckBox.IsChecked == true,
                AutoSave = AutoSaveCheckBox.IsChecked == true,
                CloudSync = CloudSyncCheckBox.IsChecked == true
            };
        }

        private static GGs.Shared.SystemIntelligence.SafetyLevel MapSafetyTagToSystemSafetyLevel(string? tag)
        {
            return tag switch
            {
                "Conservative" => GGs.Shared.SystemIntelligence.SafetyLevel.Low,
                "Balanced" => GGs.Shared.SystemIntelligence.SafetyLevel.Medium,
                "Aggressive" => GGs.Shared.SystemIntelligence.SafetyLevel.High,
                _ => GGs.Shared.SystemIntelligence.SafetyLevel.Medium
            };
        }

        private Task<bool> ValidateLicenseAsync()
        {
            // Check if user has Pro or Enterprise license
            // In production, this would integrate with license validation service
            // For now, allow access (development/testing mode)
            return Task.FromResult(true);
        }

        private async Task<bool> ValidatePrivilegesAsync()
        {
            try
            {
                if (_systemIntelligenceService != null)
                {
                    var privilegeManager = new UacPrivilegeManager(null, null);
                    var validation = await privilegeManager.ValidatePrivilegesAsync();
                    return validation.IsValid;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private async Task RequestElevationAsync()
        {
            try
            {
                var privilegeManager = new UacPrivilegeManager(null, null);
                var result = await privilegeManager.RequestElevationAsync("System Intelligence Harvester");
                
                if (result.IsSuccessful)
                {
                    // Restart application with elevation
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = System.Reflection.Assembly.GetExecutingAssembly().Location,
                        UseShellExecute = true,
                        Verb = "runas"
                    });
                    Application.Current.Shutdown();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to request elevation");
            }
        }

        private void LoadRecentProfiles()
        {
            // Load recent profiles from storage
            // This would integrate with your profile management system
            RecentProfiles.Clear();
            
            // Add sample data for demonstration
            RecentProfiles.Add(new ProfileSummaryViewModel
            {
                Name = "Gaming Optimization Profile",
                CreatedDate = DateTime.Now.AddDays(-1),
                TweakCount = 47
            });
            
            RecentProfiles.Add(new ProfileSummaryViewModel
            {
                Name = "Performance Boost Profile",
                CreatedDate = DateTime.Now.AddDays(-3),
                TweakCount = 32
            });
        }

        private void UpdateLicenseInformation()
        {
            // Update license information display
            // This would integrate with your existing license system
            CurrentTierText.Text = "Enterprise";
            ProfilesUsedText.Text = "2 / 5";
            ProfileUsageProgress.Value = 40;
        }

        private System.Windows.Media.Brush GetSafetyColor(GGs.Shared.SystemIntelligence.RiskLevel riskLevel)
        {
            return riskLevel switch
            {
                GGs.Shared.SystemIntelligence.RiskLevel.Unknown or GGs.Shared.SystemIntelligence.RiskLevel.Low => (System.Windows.Media.Brush)FindResource("SuccessGradient"),
                GGs.Shared.SystemIntelligence.RiskLevel.Medium => (System.Windows.Media.Brush)FindResource("WarningGradient"),
                GGs.Shared.SystemIntelligence.RiskLevel.High or GGs.Shared.SystemIntelligence.RiskLevel.Critical => (System.Windows.Media.Brush)FindResource("DangerGradient"),
                _ => (System.Windows.Media.Brush)FindResource("ThemeBorder")
            };
        }

        private async Task AutoSaveProfile(SystemIntelligenceProfile profile)
        {
            try
            {
                if (_systemIntelligenceService != null)
                {
                    var profileName = $"Auto-saved Profile {DateTime.Now:yyyy-MM-dd HH:mm}";
                    var desktopService = GGs.Desktop.Services.SystemIntelligenceService.Instance;
                    await desktopService.SaveProfileAsync(profile, profileName);
                    
                    Dispatcher.Invoke(() =>
                    {
                        LoadRecentProfiles(); // Refresh the list
                    });
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to auto-save profile");
            }
        }

        private void ShowLicenseUpgradeDialog()
        {
            MessageBox.Show(
                "System Intelligence Harvester is available for Pro and Enterprise users only. " +
                "Please upgrade your license to access this premium feature.",
                "License Upgrade Required",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ShowErrorMessage(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        // Event handlers for UI buttons
        private void SaveProfile_Click(object sender, RoutedEventArgs e)
        {
            if (_currentProfile != null)
            {
                // Show save dialog and save profile
                var saveDialog = new SaveProfileDialog(_currentProfile);
                saveDialog.ShowDialog();
            }
        }

        private void ShareProfile_Click(object sender, RoutedEventArgs e)
        {
            if (_currentProfile != null)
            {
                // Show share dialog
                var shareDialog = new SystemIntelligenceShareProfileDialog(_currentProfile);
                shareDialog.ShowDialog();
            }
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            // Show settings dialog
            var settingsDialog = new SystemIntelligenceSettingsDialog();
            settingsDialog.ShowDialog();
        }

        private void ViewAllProfiles_Click(object sender, RoutedEventArgs e)
        {
            // Navigate to profile management view
            var profileManager = new ProfileManagerWindow();
            profileManager.ShowDialog();
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private GGs.Shared.SystemIntelligence.RiskLevel GetRiskLevel(float estimatedRisk)
        {
            if (estimatedRisk <= 0.2f) return GGs.Shared.SystemIntelligence.RiskLevel.Low;
            if (estimatedRisk <= 0.5f) return GGs.Shared.SystemIntelligence.RiskLevel.Medium;
            if (estimatedRisk <= 0.8f) return GGs.Shared.SystemIntelligence.RiskLevel.High;
            return GGs.Shared.SystemIntelligence.RiskLevel.Critical;
        }
    }

    // Supporting view models
    public class DetectedTweakViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int ConfidenceScore { get; set; }
        public System.Windows.Media.Brush SafetyColor { get; set; } = System.Windows.Media.Brushes.Gray;
        public bool IsSelected { get; set; }
    }

    public class ProfileSummaryViewModel
    {
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public int TweakCount { get; set; }
    }

    // Event argument classes
    public class ScanProgressEventArgs : EventArgs
    {
        public double OverallProgress { get; set; }
        public string CurrentOperation { get; set; } = string.Empty;
        public double Progress { get; set; }
        public double RegistryProgress { get; set; }
        public double ServiceProgress { get; set; }
        public double BiosProgress { get; set; }
        public double GroupPolicyProgress { get; set; }
        public TimeSpan? estimatedTimeRemaining { get; set; }
    }

    public class TweakDetectedEventArgs : EventArgs
    {
        public DetectedTweak Tweak { get; set; } = null!;
    }

    public class ScanCompletedEventArgs : EventArgs
    {
        public SystemIntelligenceProfile Profile { get; set; } = null!;
    }

    public class ScanErrorEventArgs : EventArgs
    {
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception Exception { get; set; } = null!;
    }

    // Configuration class
    public class ScanConfiguration
    {
        public ScanDepth ScanDepth { get; set; }
        public GGs.Shared.SystemIntelligence.SafetyLevel SafetyLevel { get; set; }
        public bool IncludeRegistry { get; set; }
        public bool IncludeServices { get; set; }
        public bool IncludeBios { get; set; }
        public bool IncludeGroupPolicy { get; set; }
        public bool IncludeThirdParty { get; set; }
        public bool AutoSave { get; set; }
        public bool CloudSync { get; set; }
    }

    // Dialog window classes for System Intelligence operations
    public class SaveProfileDialog : Window
    {
        public SaveProfileDialog(SystemIntelligenceProfile profile) 
        {
            Title = "Save Profile";
            Width = 400;
            Height = 250;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            // Dialog content would be defined in corresponding XAML file
        }
    }

    public class SystemIntelligenceShareProfileDialog : Window
    {
        public SystemIntelligenceShareProfileDialog(SystemIntelligenceProfile profile) 
        {
            Title = "Share Profile";
            Width = 500;
            Height = 400;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            // Dialog content would be defined in corresponding XAML file
        }
    }

    public class SystemIntelligenceSettingsDialog : Window
    {
        public SystemIntelligenceSettingsDialog() 
        {
            Title = "System Intelligence Settings";
            Width = 600;
            Height = 500;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            // Dialog content would be defined in corresponding XAML file
        }
    }

    public class ProfileManagerWindow : Window
    {
        public ProfileManagerWindow() 
        {
            Title = "Profile Manager";
            Width = 800;
            Height = 600;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            // Window content would be defined in corresponding XAML file
        }
    }
}