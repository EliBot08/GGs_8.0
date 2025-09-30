using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GGs.Desktop.Services;
using GGs.Desktop.Extensions;
using CommunityProfile = GGs.Desktop.Services.CommunityProfile;
using DesktopSystemIntelligenceService = GGs.Desktop.Services.SystemIntelligenceService;
using SharedSI = GGs.Shared.SystemIntelligence;
using GGs.Shared.SystemIntelligence;

namespace GGs.Desktop.Views
{
    /// <summary>
    /// Community Hub for browsing, sharing, and managing system intelligence profiles
    /// </summary>
    public partial class CommunityHubView : UserControl, INotifyPropertyChanged
    {
        private readonly CloudProfileService _cloudPlatform;
        private readonly GGs.Shared.SystemIntelligence.SystemIntelligenceService _intelligenceService;
        private readonly UacPrivilegeManager _privilegeManager;
        private readonly GGs.Shared.SystemIntelligence.SecurityValidator _securityValidator;

        public event PropertyChangedEventHandler PropertyChanged;

        #region Properties

        private ObservableCollection<CommunityProfile> _communityProfiles = new ObservableCollection<CommunityProfile>();
        public ObservableCollection<CommunityProfile> CommunityProfiles
        {
            get => _communityProfiles;
            set
            {
                _communityProfiles = value;
                OnPropertyChanged();
            }
        }

        private ObservableCollection<CommunityProfile> _trendingProfiles = new ObservableCollection<CommunityProfile>();
        public ObservableCollection<CommunityProfile> TrendingProfiles
        {
            get => _trendingProfiles;
            set
            {
                _trendingProfiles = value;
                OnPropertyChanged();
            }
        }

        private string _searchQuery = "";
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                _searchQuery = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSearchQuery));
                _ = SearchProfilesAsync();
            }
        }

        public bool HasSearchQuery => !string.IsNullOrEmpty(SearchQuery);

        private string _selectedCategory = "All Categories";
        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                _selectedCategory = value;
                OnPropertyChanged();
                _ = ApplyFiltersAsync();
            }
        }

        private double _minRating = 0;
        public double MinRating
        {
            get => _minRating;
            set
            {
                _minRating = value;
                OnPropertyChanged();
                _ = ApplyFiltersAsync();
            }
        }

        private string _sortBy = "Most Popular";
        public string SortBy
        {
            get => _sortBy;
            set
            {
                _sortBy = value;
                OnPropertyChanged();
                _ = ApplyFiltersAsync();
            }
        }

        private bool _verifiedOnly = false;
        public bool VerifiedOnly
        {
            get => _verifiedOnly;
            set
            {
                _verifiedOnly = value;
                OnPropertyChanged();
                _ = ApplyFiltersAsync();
            }
        }

        private bool _compatibleOnly = false;
        public bool CompatibleOnly
        {
            get => _compatibleOnly;
            set
            {
                _compatibleOnly = value;
                OnPropertyChanged();
                _ = ApplyFiltersAsync();
            }
        }

        private bool _recentOnly = false;
        public bool RecentOnly
        {
            get => _recentOnly;
            set
            {
                _recentOnly = value;
                OnPropertyChanged();
                _ = ApplyFiltersAsync();
            }
        }

        private int _totalResults = 0;
        public int TotalResults
        {
            get => _totalResults;
            set
            {
                _totalResults = value;
                OnPropertyChanged();
            }
        }

        private bool _isLoading = false;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
                LoadingOverlay.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        #endregion

        public CommunityHubView()
        {
            InitializeComponent();
            DataContext = this;

            // Initialize services
            _cloudPlatform = new CloudProfileService();
            _intelligenceService = new GGs.Shared.SystemIntelligence.SystemIntelligenceService();
            _securityValidator = new SecurityValidator();
            _privilegeManager = new UacPrivilegeManager(_securityValidator, null);

            // Subscribe to events
            _cloudPlatform.ProfileDownloaded += OnProfileDownloaded;
            // _cloudPlatform.CommunityUpdated += OnCommunityUpdated; // removed: event not available on CloudProfileService

            // Load initial data
            Loaded += async (s, e) => await LoadInitialDataAsync();
        }

        #region Event Handlers

        private async void ShareProfileButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Check if user has profiles to share
                var localProfiles = await _intelligenceService.GetLocalProfilesAsync();
                if (!localProfiles.Any())
                {
                    MessageBox.Show("You need to create a system intelligence profile first before sharing.", 
                                  "No Profiles Found", 
                                  MessageBoxButton.OK, 
                                  MessageBoxImage.Information);
                    return;
                }

                // Open share dialog
                var shareDialog = new CommunityShareProfileDialog(localProfiles);
                 if (shareDialog.ShowDialog() == true)
                 {
                     IsLoading = true;
                     
                     var selectedProfile = shareDialog.SelectedProfile;
                     var shareOptions = shareDialog.ShareOptions;
                    
                    var settings = new ProfileSettings
                    {
                        ProfileId = Guid.NewGuid().ToString(),
                        Name = selectedProfile.Name,
                        Author = Environment.UserName,
                        GameSettings = new GameSettings { GameName = "All Games", ProcessPriority = "Normal", DisableFullscreenOptimizations = false, UseGameMode = true },
                        SystemSettings = new SystemSettings { PowerPlan = "Balanced", NetworkOptimizations = true, MemoryOptimizations = true }
                    };

                    var ok = await _cloudPlatform.UploadProfileAsync(selectedProfile.Name, "Shared via Community", settings);
                    
                    if (ok)
                    {
                        MessageBox.Show($"Profile shared successfully!", 
                                      "Profile Shared", 
                                      MessageBoxButton.OK, 
                                      MessageBoxImage.Information);
                        
                        // Refresh the community profiles
                        await LoadCommunityProfilesAsync();
                    }
                    else
                    {
                        MessageBox.Show($"Failed to share profile.", 
                                      "Share Failed", 
                                      MessageBoxButton.OK, 
                                      MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sharing profile: {ex.Message}", 
                              "Error", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async void MyProfilesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Open my profiles dialog
                var myProfilesDialog = new MyProfilesDialog(_cloudPlatform, _intelligenceService);
                myProfilesDialog.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening my profiles: {ex.Message}", 
                              "Error", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Error);
            }
        }

        private async void DownloadProfile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.Tag is CommunityProfile profile)
                {
                    // Check privileges
                    var hasPrivileges = await _privilegeManager.HasRequiredPrivilegesAsync();
                    if (!hasPrivileges)
                    {
                        var elevationResult = await _privilegeManager.RequestElevationAsync("Download and apply system intelligence profile");
                        if (!elevationResult.IsSuccessful)
                        {
                            MessageBox.Show("Administrator privileges are required to download and apply profiles.", 
                                          "Privileges Required", 
                                          MessageBoxButton.OK, 
                                          MessageBoxImage.Warning);
                            return;
                        }
                    }

                    // Confirm download
                    var result = MessageBox.Show($"Download and apply profile '{profile.Name}'?\n\nThis will apply optimized settings to your system.", 
                                               "Confirm Download", 
                                               MessageBoxButton.YesNo, 
                                               MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        IsLoading = true;
                        
                        var ok = await _cloudPlatform.DownloadProfileAsync(profile);
                        
                        if (ok)
                        {
                            MessageBox.Show($"Profile '{profile.Name}' downloaded and applied successfully!", 
                                          "Download Complete", 
                                          MessageBoxButton.OK, 
                                          MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show($"Failed to download profile.", 
                                          "Download Failed", 
                                          MessageBoxButton.OK, 
                                          MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error downloading profile: {ex.Message}", 
                              "Error", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ViewDetails_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.Tag is CommunityProfile profile)
                {
                    // Open profile details dialog
                    var detailsDialog = new ProfileDetailsDialog(profile, _cloudPlatform);
                    detailsDialog.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error viewing profile details: {ex.Message}", 
                              "Error", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Error);
            }
        }

        private void ProfileCard_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (sender is Border border && border.Tag is CommunityProfile profile)
                {
                    // Open profile details dialog
                    var detailsDialog = new ProfileDetailsDialog(profile, _cloudPlatform);
                    detailsDialog.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error viewing profile details: {ex.Message}", 
                              "Error", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Error);
            }
        }

        private void OnProfileDownloaded(object sender, ProfileDownloadedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                // Update UI to reflect downloaded profile
                // Could show notification or update local profiles list
            });
        }

        private void OnCommunityUpdated(object sender, EventArgs e)
        {
            Dispatcher.Invoke(async () =>
            {
                // Refresh community data when updates occur
                await LoadCommunityProfilesAsync();
            });
        }

        #endregion

        #region Data Loading

        private async Task LoadInitialDataAsync()
        {
            try
            {
                IsLoading = true;
                
                // Load trending profiles
                await LoadTrendingProfilesAsync();
                
                // Load community profiles
                await LoadCommunityProfilesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading community data: {ex.Message}", 
                              "Error", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadTrendingProfilesAsync()
        {
            try
            {
                var list = await _cloudPlatform.GetMarketplaceProfilesAsync();
                
                TrendingProfiles.Clear();
                foreach (var p in list.OrderByDescending(p => p.Downloads).Take(5))
                {
                    TrendingProfiles.Add(p);
                }
            }
            catch (Exception ex)
            {
                // Log error but don't show to user for trending section
                System.Diagnostics.Debug.WriteLine($"Error loading trending profiles: {ex.Message}");
            }
        }

        private async Task LoadCommunityProfilesAsync()
        {
            try
            {
                var category = SelectedCategory != "All Categories" ? SelectedCategory : null;
                var list = await _cloudPlatform.GetMarketplaceProfilesAsync(category);
                
                CommunityProfiles.Clear();
                foreach (var profile in list)
                {
                    CommunityProfiles.Add(profile);
                }
                
                TotalResults = list.Count;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading community profiles: {ex.Message}", 
                              "Error", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Error);
            }
        }

        private async Task SearchProfilesAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(SearchQuery))
                {
                    await LoadCommunityProfilesAsync();
                    return;
                }

                IsLoading = true;
                
                var all = await _cloudPlatform.GetMarketplaceProfilesAsync();
                var q = SearchQuery;
                var filtered = all.Where(p =>
                    (!string.IsNullOrEmpty(p.Name) && p.Name.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(p.Description) && p.Description.Contains(q, StringComparison.OrdinalIgnoreCase))
                );

                if (MinRating > 0)
                {
                    filtered = filtered.Where(p => p.Rating >= MinRating);
                }
                if (SelectedCategory != "All Categories")
                {
                    filtered = filtered.Where(p => p.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase));
                }

                CommunityProfiles.Clear();
                foreach (var profile in filtered)
                {
                    CommunityProfiles.Add(profile);
                }
                
                TotalResults = CommunityProfiles.Count;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error searching profiles: {ex.Message}", 
                              "Error", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ApplyFiltersAsync()
        {
            try
            {
                IsLoading = true;
                
                if (!string.IsNullOrEmpty(SearchQuery))
                {
                    await SearchProfilesAsync();
                }
                else
                {
                    await LoadCommunityProfilesAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error applying filters: {ex.Message}", 
                              "Error", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private object CreateBrowseFilter()
        {
            return new object();
        }

        #endregion

        #region INotifyPropertyChanged

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region Cleanup

        public void Dispose()
        {
            _cloudPlatform?.Dispose();
        }

        #endregion
    }

    #region Supporting Dialog Classes

    public class ShareOptions {}

    /// <summary>
    /// Dialog for sharing a profile to the community
    /// </summary>
    public class CommunityShareProfileDialog : Window
    {
        public SharedSI.SystemIntelligenceProfile SelectedProfile { get; private set; }
        public ShareOptions ShareOptions { get; private set; }
    
        public CommunityShareProfileDialog(System.Collections.Generic.List<SharedSI.SystemIntelligenceProfile> profiles)
        {
            // Implementation for share dialog
            Title = "Share Profile to Community";
            Width = 500;
            Height = 400;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
    }

    /// <summary>
    /// Dialog for managing user's shared profiles
    /// </summary>
    public class MyProfilesDialog : Window
    {
        public MyProfilesDialog(CloudProfileService cloudPlatform, GGs.Shared.SystemIntelligence.SystemIntelligenceService intelligenceService)
        {
            // Implementation for my profiles dialog
            Title = "My Shared Profiles";
            Width = 800;
            Height = 600;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
    }

    /// <summary>
    /// Dialog for viewing detailed profile information
    /// </summary>
    public class ProfileDetailsDialog : Window
    {
        public ProfileDetailsDialog(CommunityProfile profile, CloudProfileService cloudPlatform)
        {
            // Implementation for profile details dialog
            Title = $"Profile Details - {profile.Name}";
            Width = 700;
            Height = 500;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
    }

    #endregion
}