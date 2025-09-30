using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using GGs.Desktop.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using GGs.Shared.SystemIntelligence;
// using GGs.Shared.SystemIntelligence.Models; // removed - namespace not present
// using GGs.Shared.SystemIntelligence.Enums; // Commented out to avoid ambiguity with GGs.Shared.Enums
using GGs.Shared.Enums;
using DesktopIntelligenceService = GGs.Desktop.Services.SystemIntelligenceService;

namespace GGs.Desktop.Views
{
    /// <summary>
    /// Profile Architect - Enterprise-grade profile management system
    /// Provides comprehensive tools for organizing, sharing, and managing System Intelligence profiles
    /// </summary>
    public partial class ProfileArchitectView : UserControl, INotifyPropertyChanged
    {
        private ILogger<ProfileArchitectView>? _logger;
        private DesktopIntelligenceService? _systemIntelligenceService;
        private GGs.Shared.SystemIntelligence.CloudProfileManager? _cloudProfileManager;
        
        private ObservableCollection<ProfileViewModel> _allProfiles;
        private ObservableCollection<ProfileViewModel> _filteredProfiles;
        private string _searchText = "";
        private ProfileFilter _currentFilter = ProfileFilter.All;
        private ProfileSortOrder _currentSort = ProfileSortOrder.DateDesc;

        public ObservableCollection<ProfileViewModel> FilteredProfiles
        {
            get => _filteredProfiles;
            set
            {
                _filteredProfiles = value;
                OnPropertyChanged(nameof(FilteredProfiles));
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged(nameof(SearchText));
                ApplyFilters();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public ProfileArchitectView()
        {
            InitializeComponent();
            
            _allProfiles = new ObservableCollection<ProfileViewModel>();
            _filteredProfiles = new ObservableCollection<ProfileViewModel>();
            
            DataContext = this;
            ProfilesGridView.ItemsSource = FilteredProfiles;
            ProfilesListView.ItemsSource = FilteredProfiles;

            InitializeServices();
            LoadProfilesAsync();
            UpdateLicenseInformation();
        }

        private void InitializeServices()
        {
            try
            {
                var serviceProvider = App.ServiceProvider;
                _logger = serviceProvider?.GetService<ILogger<ProfileArchitectView>>();
                _systemIntelligenceService = serviceProvider?.GetService<DesktopIntelligenceService>();
                _cloudProfileManager = serviceProvider?.GetService<CloudProfileManager>();
                
                // CloudProfileManager events not available in Shared version
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to initialize services");
            }
        }

        private async void LoadProfilesAsync()
        {
            try
            {
                ShowLoading("Loading profiles...");

                // Load local profiles
                var localProfiles = await LoadLocalProfilesAsync();
                
                // Load cloud profiles if available
                var cloudProfiles = await LoadCloudProfilesAsync();

                // Combine and convert to view models
                var allProfileData = localProfiles.Concat(cloudProfiles).ToList();
                
                _allProfiles.Clear();
                foreach (var profile in allProfileData)
                {
                    _allProfiles.Add(new ProfileViewModel(profile));
                }

                ApplyFilters();
                UpdateStatistics();
                HideLoading();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load profiles");
                HideLoading();
                ShowErrorMessage("Failed to load profiles", ex.Message);
            }
        }

        private async Task<List<SystemIntelligenceProfile>> LoadLocalProfilesAsync()
        {
            try
            {
                if (_systemIntelligenceService != null)
                {
                    return await _systemIntelligenceService.GetSavedProfilesAsync();
                }
                return new List<SystemIntelligenceProfile>();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load local profiles");
                return new List<SystemIntelligenceProfile>();
            }
        }

        private async Task<List<SystemIntelligenceProfile>> LoadCloudProfilesAsync()
        {
            try
            {
                if (_cloudProfileManager != null)
                {
                    var cloudProfiles = await _cloudProfileManager.GetUserProfilesAsync();
                    return cloudProfiles.ToList();
                }
                return new List<SystemIntelligenceProfile>();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load cloud profiles");
                return new List<SystemIntelligenceProfile>();
            }
        }

        private void ApplyFilters()
        {
            try
            {
                var filtered = _allProfiles.AsEnumerable();

                // Apply search filter
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    var searchLower = SearchText.ToLower();
                    filtered = filtered.Where(p => 
                        p.Name.ToLower().Contains(searchLower) ||
                        p.Description.ToLower().Contains(searchLower) ||
                        p.Tags.Any(tag => tag.ToLower().Contains(searchLower)));
                }

                // Apply category filter
                filtered = _currentFilter switch
                {
                    ProfileFilter.Local => filtered.Where(p => !p.IsCloudProfile),
                    ProfileFilter.Shared => filtered.Where(p => p.IsShared),
                    ProfileFilter.Favorites => filtered.Where(p => p.IsFavorite),
                    _ => filtered
                };

                // Apply sorting
                filtered = _currentSort switch
                {
                    ProfileSortOrder.DateAsc => filtered.OrderBy(p => p.CreatedDate),
                    ProfileSortOrder.DateDesc => filtered.OrderByDescending(p => p.CreatedDate),
                    ProfileSortOrder.NameAsc => filtered.OrderBy(p => p.Name),
                    ProfileSortOrder.NameDesc => filtered.OrderByDescending(p => p.Name),
                    ProfileSortOrder.TweaksDesc => filtered.OrderByDescending(p => p.TweakCount),
                    _ => filtered.OrderByDescending(p => p.CreatedDate)
                };

                FilteredProfiles.Clear();
                foreach (var profile in filtered)
                {
                    FilteredProfiles.Add(profile);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to apply filters");
            }
        }

        private void UpdateStatistics()
        {
            try
            {
                TotalProfilesText.Text = _allProfiles.Count.ToString();
                SharedProfilesText.Text = _allProfiles.Count(p => p.IsShared).ToString();
                TotalTweaksText.Text = _allProfiles.Sum(p => p.TweakCount).ToString();
                
                // Calculate cloud storage usage (mock calculation)
                var totalSize = _allProfiles.Sum(p => p.EstimatedSize);
                CloudStorageText.Text = FormatFileSize(totalSize);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to update statistics");
            }
        }

        private void UpdateLicenseInformation()
        {
            try
            {
                // This would integrate with your license system
                var currentTier = "Enterprise"; // Get from license service
                var usedProfiles = _allProfiles?.Count ?? 0;
                var maxProfiles = currentTier == "Enterprise" ? 5 : 2;

                ProfileUsageText.Text = $"{usedProfiles} / {maxProfiles} Profiles Used";
                ProfileUsageProgress.Value = (double)usedProfiles / maxProfiles * 100;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to update license information");
            }
        }

        // Event Handlers
        private async void CreateProfile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var createDialog = new CreateProfileDialog();
                if (createDialog.ShowDialog() == true)
                {
                    ShowLoading("Creating new profile...");
                    
                    // Create new profile based on dialog input
                    var newProfile = await CreateNewProfileAsync(createDialog.ProfileName, createDialog.ProfileDescription);
                    
                    if (newProfile != null)
                    {
                        _allProfiles.Add(new ProfileViewModel(newProfile));
                        ApplyFilters();
                        UpdateStatistics();
                    }
                    
                    HideLoading();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to create profile");
                HideLoading();
                ShowErrorMessage("Failed to create profile", ex.Message);
            }
        }

        private async void ImportProfile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Import System Intelligence Profile",
                    Filter = "Profile Files (*.ggsprofile)|*.ggsprofile|All Files (*.*)|*.*",
                    Multiselect = true
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    ShowLoading("Importing profiles...");
                    
                    foreach (var fileName in openFileDialog.FileNames)
                    {
                        var importedProfile = await ImportProfileFromFileAsync(fileName);
                        if (importedProfile != null)
                        {
                            _allProfiles.Add(new ProfileViewModel(importedProfile));
                        }
                    }
                    
                    ApplyFilters();
                    UpdateStatistics();
                    HideLoading();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to import profile");
                HideLoading();
                ShowErrorMessage("Failed to import profile", ex.Message);
            }
        }

        private async void CloudSync_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_cloudProfileManager != null)
                {
                    ShowLoading("Synchronizing with cloud...");
                    await _cloudProfileManager.SyncProfilesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to sync with cloud");
                HideLoading();
                ShowErrorMessage("Cloud sync failed", ex.Message);
            }
        }

        private async void ApplyProfile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                var profile = button?.DataContext as ProfileViewModel;
                
                if (profile != null)
                {
                    var confirmResult = MessageBox.Show(
                        $"Are you sure you want to apply the profile '{profile.Name}'?\n\n" +
                        $"This will apply {profile.TweakCount} tweaks to your system.",
                        "Confirm Profile Application",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (confirmResult == MessageBoxResult.Yes)
                    {
                        ShowLoading($"Applying profile '{profile.Name}'...");
                        
                        var success = await ApplyProfileAsync(profile);
                        
                        HideLoading();
                        
                        if (success)
                        {
                            MessageBox.Show(
                                $"Profile '{profile.Name}' has been successfully applied!",
                                "Profile Applied",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to apply profile");
                HideLoading();
                ShowErrorMessage("Failed to apply profile", ex.Message);
            }
        }

        private async void ShareProfile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                var profile = button?.DataContext as ProfileViewModel;
                
                if (profile != null)
                {
                    var shareDialog = new ShareProfileViewModelDialog(profile);
                    if (shareDialog.ShowDialog() == true)
                    {
                        ShowLoading("Sharing profile...");
                        
                        var success = await ShareProfileAsync(profile, new ShareSettings());
                        
                        HideLoading();
                        
                        if (success)
                        {
                            profile.IsShared = true;
                            MessageBox.Show(
                                $"Profile '{profile.Name}' has been shared successfully!",
                                "Profile Shared",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to share profile");
                HideLoading();
                ShowErrorMessage("Failed to share profile", ex.Message);
            }
        }

        private void ProfileMenu_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                var profile = button?.DataContext as ProfileViewModel;
                
                if (profile != null)
                {
                    var contextMenu = new ContextMenu();
                    
                    // Edit
                    var editItem = new MenuItem { Header = "✏️ Edit Profile" };
                    editItem.Click += (s, args) => EditProfile(profile);
                    contextMenu.Items.Add(editItem);
                    
                    // Duplicate
                    var duplicateItem = new MenuItem { Header = "📋 Duplicate" };
                    duplicateItem.Click += (s, args) => DuplicateProfile(profile);
                    contextMenu.Items.Add(duplicateItem);
                    
                    // Export
                    var exportItem = new MenuItem { Header = "📤 Export" };
                    exportItem.Click += (s, args) => ExportProfile(profile);
                    contextMenu.Items.Add(exportItem);
                    
                    contextMenu.Items.Add(new Separator());
                    
                    // Favorite/Unfavorite
                    var favoriteItem = new MenuItem 
                    { 
                        Header = profile.IsFavorite ? "💔 Remove from Favorites" : "❤️ Add to Favorites" 
                    };
                    favoriteItem.Click += (s, args) => ToggleFavorite(profile);
                    contextMenu.Items.Add(favoriteItem);
                    
                    contextMenu.Items.Add(new Separator());
                    
                    // Delete
                    var deleteItem = new MenuItem { Header = "🗑️ Delete Profile" };
                    deleteItem.Click += (s, args) => DeleteProfile(profile);
                    contextMenu.Items.Add(deleteItem);
                    
                    contextMenu.PlacementTarget = button;
                    contextMenu.IsOpen = true;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to show profile menu");
            }
        }

        private void Filter_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Uncheck all filter buttons
                FilterAll.IsChecked = false;
                FilterLocal.IsChecked = false;
                FilterShared.IsChecked = false;
                FilterFavorites.IsChecked = false;

                // Check the clicked button and set filter
                var button = sender as ToggleButton;
                if (button != null) button.IsChecked = true;

                _currentFilter = button?.Name switch
                {
                    nameof(FilterLocal) => ProfileFilter.Local,
                    nameof(FilterShared) => ProfileFilter.Shared,
                    nameof(FilterFavorites) => ProfileFilter.Favorites,
                    _ => ProfileFilter.All
                };

                ApplyFilters();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to apply filter");
            }
        }

        private void Sort_Changed(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var comboBox = sender as ComboBox;
                var selectedItem = comboBox?.SelectedItem as ComboBoxItem;
                var sortTag = selectedItem?.Tag?.ToString();

                if (Enum.TryParse<ProfileSortOrder>(sortTag, out var sortOrder))
                {
                    _currentSort = sortOrder;
                    ApplyFilters();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to change sort order");
            }
        }

        private void ViewToggle_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as ToggleButton;
                
                if (button == GridViewToggle)
                {
                    GridViewToggle.IsChecked = true;
                    ListViewToggle.IsChecked = false;
                    ProfilesGridView.Visibility = Visibility.Visible;
                    ProfilesListView.Visibility = Visibility.Collapsed;
                }
                else
                {
                    GridViewToggle.IsChecked = false;
                    ListViewToggle.IsChecked = true;
                    ProfilesGridView.Visibility = Visibility.Collapsed;
                    ProfilesListView.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to toggle view");
            }
        }

        private void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        // Cloud sync event handlers
        private void OnCloudSyncProgress(object sender, CloudSyncProgressEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                LoadingText.Text = $"Cloud sync: {e.CurrentOperation} ({e.Progress:F0}%)";
            });
        }

        private void OnCloudSyncCompleted(object sender, CloudSyncCompletedEventArgs e)
        {
            Dispatcher.Invoke(async () =>
            {
                await Task.CompletedTask; // Suppress CS1998
                HideLoading();
                
                if (e.IsSuccessful)
                {
                    LoadProfilesAsync(); // Refresh profiles
                    MessageBox.Show("Cloud synchronization completed successfully!", "Sync Complete", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    ShowErrorMessage("Cloud sync failed", e.ErrorMessage);
                }
            });
        }

        // Helper methods
        private async Task<SystemIntelligenceProfile?> CreateNewProfileAsync(string name, string description)
        {
            try
            {
                if (_systemIntelligenceService != null)
                {
                    var newProfile = new SystemIntelligenceProfile
                    {
                        Id = Guid.NewGuid(),
                        Name = name,
                        Description = description,
                        CreatedDate = DateTime.Now,
                        DetectedTweaks = new List<DetectedTweak>()
                    };

                    await _systemIntelligenceService.SaveProfileAsync(newProfile, name);
                    return newProfile;
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to create new profile");
                return null;
            }
        }

        private async Task<SystemIntelligenceProfile?> ImportProfileFromFileAsync(string fileName)
        {
            try
            {
                if (_systemIntelligenceService != null)
                {
                    return await _systemIntelligenceService.ImportProfileAsync(fileName);
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to import profile from file: {FileName}", fileName);
                return null;
            }
        }

        private async Task<bool> ApplyProfileAsync(ProfileViewModel profile)
        {
            try
            {
                if (_systemIntelligenceService != null)
                {
                    var systemProfile = await _systemIntelligenceService.LoadProfileAsync(profile.Id);
                    if (systemProfile != null)
                    {
                        return await _systemIntelligenceService.ApplyProfileAsync(systemProfile);
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to apply profile: {ProfileName}", profile.Name);
                return false;
            }
        }

        private async Task<bool> ShareProfileAsync(ProfileViewModel profile, ShareSettings shareSettings)
        {
            try
            {
                if (_cloudProfileManager != null && _systemIntelligenceService != null)
                {
                    var systemProfile = await _systemIntelligenceService.LoadProfileAsync(profile.Id);
                    if (systemProfile != null)
                    {
                        return await _cloudProfileManager.ShareProfileAsync(systemProfile.Name, shareSettings.Permission.ToString());
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to share profile: {ProfileName}", profile.Name);
                return false;
            }
        }

        private void EditProfile(ProfileViewModel profile)
        {
            var editDialog = new EditProfileDialog(profile);
            editDialog.ShowDialog();
        }

        private async void DuplicateProfile(ProfileViewModel profile)
        {
            try
            {
                ShowLoading("Duplicating profile...");
                
                var duplicatedProfile = await CreateNewProfileAsync(
                    $"{profile.Name} (Copy)", 
                    profile.Description);
                
                if (duplicatedProfile != null)
                {
                    _allProfiles.Add(new ProfileViewModel(duplicatedProfile));
                    ApplyFilters();
                    UpdateStatistics();
                }
                
                HideLoading();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to duplicate profile");
                HideLoading();
            }
        }

        private async void ExportProfile(ProfileViewModel profile)
        {
            try
            {
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Export Profile",
                    Filter = "Profile Files (*.ggsprofile)|*.ggsprofile",
                    FileName = $"{profile.Name}.ggsprofile"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    ShowLoading("Exporting profile...");
                    
                    if (_systemIntelligenceService != null)
                    {
                        var systemProfile = await _systemIntelligenceService.LoadProfileAsync(profile.Id);
                        if (systemProfile != null)
                        {
                            await _systemIntelligenceService.ExportProfileAsync(systemProfile, saveFileDialog.FileName);
                        }
                    }
                    
                    HideLoading();
                    MessageBox.Show("Profile exported successfully!", "Export Complete", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to export profile");
                HideLoading();
                ShowErrorMessage("Export failed", ex.Message);
            }
        }

        private void ToggleFavorite(ProfileViewModel profile)
        {
            profile.IsFavorite = !profile.IsFavorite;
            // Save favorite status to storage
            ApplyFilters(); // Refresh if favorites filter is active
        }

        private async void DeleteProfile(ProfileViewModel profile)
        {
            try
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to delete the profile '{profile.Name}'?\n\nThis action cannot be undone.",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    ShowLoading("Deleting profile...");
                    
                    if (_systemIntelligenceService != null)
                    {
                        await _systemIntelligenceService.DeleteProfileAsync(profile.Id);
                    }
                    
                    _allProfiles.Remove(profile);
                    ApplyFilters();
                    UpdateStatistics();
                    
                    HideLoading();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to delete profile");
                HideLoading();
                ShowErrorMessage("Delete failed", ex.Message);
            }
        }

        private void ShowLoading(string message)
        {
            LoadingText.Text = message;
            LoadingOverlay.Visibility = Visibility.Visible;
        }

        private void HideLoading()
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        private void ShowErrorMessage(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
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

    // Supporting classes and enums
    public class ProfileViewModel : INotifyPropertyChanged
    {
        private bool _isFavorite;
        private bool _isShared;

        public string Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; }
        public DateTime CreatedDate { get; set; }
        public int TweakCount { get; set; }
        public int SafetyScore { get; set; }
        public System.Windows.Media.Brush SafetyColor { get; set; }
        public bool IsCloudProfile { get; set; }
        public long EstimatedSize { get; set; }
        public List<string> Tags { get; set; }
        public string StatusIcon => IsCloudProfile ? "☁️" : "💾";

        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                _isFavorite = value;
                OnPropertyChanged(nameof(IsFavorite));
            }
        }

        public bool IsShared
        {
            get => _isShared;
            set
            {
                _isShared = value;
                OnPropertyChanged(nameof(IsShared));
            }
        }

        public ProfileViewModel(SystemIntelligenceProfile profile)
        {
            Id = profile.Id.ToString();
            Name = profile.Name;
            Description = profile.Description;
            CreatedDate = profile.CreatedDate;
            TweakCount = profile.DetectedTweaks?.Count ?? 0;
            SafetyScore = CalculateSafetyScore(profile);
            SafetyColor = GetSafetyColor(SafetyScore);
            // No CloudProfileId on SystemIntelligenceProfile; infer cloud via Status (Generated == local, others considered cloud)
            IsCloudProfile = profile.Status != ScanStatus.Completed;
            EstimatedSize = EstimateProfileSize(profile);
            Tags = ExtractTags(profile);
        }

        private GGs.Shared.SystemIntelligence.RiskLevel GetRiskLevel(float estimatedRisk)
        {
            if (estimatedRisk <= 0.2f) return GGs.Shared.SystemIntelligence.RiskLevel.Low;
            if (estimatedRisk <= 0.5f) return GGs.Shared.SystemIntelligence.RiskLevel.Medium;
            if (estimatedRisk <= 0.8f) return GGs.Shared.SystemIntelligence.RiskLevel.High;
            return GGs.Shared.SystemIntelligence.RiskLevel.Critical;
        }

        private int CalculateSafetyScore(SystemIntelligenceProfile profile)
        {
            if (profile.DetectedTweaks == null || !profile.DetectedTweaks.Any())
                return 100;

            var totalTweaks = profile.DetectedTweaks.Count;
            var safeTweaks = profile.DetectedTweaks.Count(t => GetRiskLevel(t.EstimatedRisk) <= GGs.Shared.SystemIntelligence.RiskLevel.Low);
            
            return (int)((double)safeTweaks / totalTweaks * 100);
        }

        private System.Windows.Media.Brush GetSafetyColor(int safetyScore)
        {
            return safetyScore switch
            {
                >= 80 => System.Windows.Media.Brushes.Green,
                >= 60 => System.Windows.Media.Brushes.Orange,
                _ => System.Windows.Media.Brushes.Red
            };
        }

        private long EstimateProfileSize(SystemIntelligenceProfile profile)
        {
            // Rough estimation based on tweak count and metadata
            var baseSize = 1024; // 1KB base
            var tweakSize = (profile.DetectedTweaks?.Count ?? 0) * 512; // 512 bytes per tweak
            return baseSize + tweakSize;
        }

        private List<string> ExtractTags(SystemIntelligenceProfile profile)
        {
            var tags = new List<string>();
            
            if (profile.DetectedTweaks != null)
            {
                var categories = profile.DetectedTweaks
                    .Select(t => t.Category.ToString())
                    .Distinct()
                    .Take(3);
                tags.AddRange(categories);
            }
            
            return tags;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public enum ProfileFilter
    {
        All,
        Local,
        Shared,
        Favorites
    }

    public enum ProfileSortOrder
    {
        DateAsc,
        DateDesc,
        NameAsc,
        NameDesc,
        TweaksDesc
    }

    // Event argument classes
    public class CloudSyncProgressEventArgs : EventArgs
    {
        public string CurrentOperation { get; set; } = string.Empty;
        public double Progress { get; set; }
    }

    public class CloudSyncCompletedEventArgs : EventArgs
    {
        public bool IsSuccessful { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    // Dialog window classes for Profile Architect operations
    public class CreateProfileDialog : Window
    {
        public string ProfileName { get; set; } = string.Empty;
        public string ProfileDescription { get; set; } = string.Empty;

        public CreateProfileDialog()
        {
            Title = "Create New Profile";
            Width = 450;
            Height = 300;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            // Dialog content would be defined in corresponding XAML file
        }
    }

    public class EditProfileDialog : Window
    {
        public ProfileViewModel Profile { get; set; }
        
        public EditProfileDialog(ProfileViewModel profile)
        {
            Profile = profile;
            Title = $"Edit Profile: {profile.Name}";
            Width = 500;
            Height = 400;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            // Dialog content would be defined in corresponding XAML file
        }
    }

    public class ArchitectShareProfileDialog : Window
    {
        public ShareSettings ShareSettings { get; set; } = new();
        
        public ArchitectShareProfileDialog(ProfileViewModel profile)
        {
            Title = $"Share Profile: {profile.Name}";
            Width = 550;
            Height = 450;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            // Dialog content would be defined in corresponding XAML file
        }
    }

    public class ShareSettings
    {
        public SharingPermission Permission { get; set; } = SharingPermission.FriendsOnly;
        public bool AllowComments { get; set; } = true;
        public bool AllowRating { get; set; } = true;
    }

    public class ShareProfileViewModelDialog : Window
    {
        public ShareProfileViewModelDialog(ProfileViewModel profile) 
        {
            Title = $"Share Profile: {profile.Name}";
            Width = 550;
            Height = 450;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            // Dialog content would be defined in corresponding XAML file
        }
    }
}