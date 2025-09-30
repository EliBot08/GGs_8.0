using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using GGs.Desktop.Services;
using GGs.Shared.Tweaks;

namespace GGs.Desktop.ViewModels.Admin
{
    /// <summary>
    /// Enterprise Cloud Profiles ViewModel with signature trust and security validation
    /// </summary>
    public class CloudProfilesViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly CloudProfileService _cloudProfileService;
        private readonly SignatureValidationService _signatureValidationService;
        private ObservableCollection<CloudProfile> _cloudProfiles;
        private ObservableCollection<CloudProfile> _filteredProfiles;
        private string _searchQuery = string.Empty;
        private string _loadingStatus = "Ready";
        private bool _isAdmin;

        public CloudProfilesViewModel()
        {
            _cloudProfileService = new CloudProfileService();
            _signatureValidationService = new SignatureValidationService();
            _cloudProfiles = new ObservableCollection<CloudProfile>();
            _filteredProfiles = new ObservableCollection<CloudProfile>();
            
            InitializeCommands();
            _ = LoadCloudProfilesAsync();
        }

        #region Properties

        public ObservableCollection<CloudProfile> CloudProfiles
        {
            get => _filteredProfiles;
            set
            {
                _filteredProfiles = value;
                OnPropertyChanged();
            }
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                _searchQuery = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        public string? SelectedCategory { get; set; }
        public string? SelectedTrustLevel { get; set; }
        public string? SortBy { get; set; }
        public bool ShowOnlyApproved { get; set; }

        public string LoadingStatus
        {
            get => _loadingStatus;
            set
            {
                _loadingStatus = value;
                OnPropertyChanged();
            }
        }

        public bool IsAdmin
        {
            get => _isAdmin;
            set
            {
                _isAdmin = value;
                OnPropertyChanged();
            }
        }

        public int TotalProfiles => _cloudProfiles.Count;
        public int SecurityWarningCount => _cloudProfiles.Count(p => p.HasSignatureWarning);
        public bool HasSecurityWarnings => SecurityWarningCount > 0;
        public int TrustedPublisherCount => _cloudProfiles.Select(p => p.PublisherName).Distinct().Count();

        #endregion

        #region Commands

        public ICommand DownloadProfileCommand { get; private set; }
        public ICommand VerifySignatureCommand { get; private set; }
        public ICommand ApproveProfileCommand { get; private set; }

        private void InitializeCommands()
        {
            DownloadProfileCommand = new RelayCommand<CloudProfile>(async profile => await DownloadProfileAsync(profile));
            VerifySignatureCommand = new RelayCommand<CloudProfile>(async profile => await VerifySignatureAsync(profile));
            ApproveProfileCommand = new RelayCommand<CloudProfile>(async profile => await ApproveProfileAsync(profile));
        }

        #endregion

        #region Methods

        public async Task LoadCloudProfilesAsync()
        {
            try
            {
                LoadingStatus = "Loading cloud profiles...";
                
                var profiles = await _cloudProfileService.GetAvailableProfilesAsync();
                
                // Convert CommunityProfile to CloudProfile and validate signatures
                _cloudProfiles.Clear();
                foreach (var communityProfile in profiles)
                {
                    var cloudProfile = ConvertToCloudProfile(communityProfile);
                    await ValidateProfileTrust(cloudProfile);
                    _cloudProfiles.Add(cloudProfile);
                }
                
                ApplyFilters();
                LoadingStatus = "Ready";
                
                OnPropertyChanged(nameof(TotalProfiles));
                OnPropertyChanged(nameof(SecurityWarningCount));
                OnPropertyChanged(nameof(HasSecurityWarnings));
                OnPropertyChanged(nameof(TrustedPublisherCount));
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Failed to load cloud profiles", ex);
                LoadingStatus = "Error loading profiles";
                throw;
            }
        }

        public async Task RefreshProfilesAsync()
        {
            await LoadCloudProfilesAsync();
        }

        public async Task<(bool Success, string Message)> UploadProfileAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return (false, "File not found");
                }

                var result = await _cloudProfileService.UploadProfileAsync(filePath);
                
                if (result.Success)
                {
                    await RefreshProfilesAsync();
                }
                
                return result;
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Failed to upload cloud profile", ex);
                return (false, $"Upload failed: {ex.Message}");
            }
        }

        private async Task DownloadProfileAsync(CloudProfile profile)
        {
            try
            {
                // Verify signature before download if trust is enabled
                var trustSettings = _signatureValidationService.GetTrustSettings();
                if (trustSettings.RequireSignatureValidation && !profile.IsVerified)
                {
                    var verificationResult = await VerifySignatureAsync(profile);
                    if (!verificationResult)
                    {
                        return; // Don't download if signature verification fails
                    }
                }

                var result = await _cloudProfileService.DownloadProfileAsync(profile.Id);
                
                if (result.Success)
                {
                    AppLogger.LogInfo($"Successfully downloaded cloud profile: {profile.Name}");
                    ProfileDownloaded?.Invoke(this, new CloudProfileEventArgs(profile));
                }
                else
                {
                    AppLogger.LogWarn($"Failed to download cloud profile {profile.Name}: {result.Message}");
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"Error downloading cloud profile: {profile.Name}", ex);
            }
        }

        private async Task<bool> VerifySignatureAsync(CloudProfile profile)
        {
            try
            {
                var isVerified = await _signatureValidationService.VerifyProfileSignatureAsync(profile);
                
                profile.IsVerified = isVerified;
                profile.HasSignatureWarning = !isVerified;
                
                TrustVerificationCompleted?.Invoke(this, new TrustVerificationEventArgs(
                    profile, 
                    isVerified, 
                    isVerified ? null : "Digital signature could not be verified"
                ));
                
                return isVerified;
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"Error verifying signature for profile: {profile.Name}", ex);
                
                TrustVerificationCompleted?.Invoke(this, new TrustVerificationEventArgs(
                    profile, 
                    false, 
                    $"Signature verification error: {ex.Message}"
                ));
                
                return false;
            }
        }

        private async Task ApproveProfileAsync(CloudProfile profile)
        {
            try
            {
                if (!IsAdmin)
                {
                    AppLogger.LogWarn("Non-admin user attempted to approve cloud profile");
                    return;
                }

                var result = await _cloudProfileService.ApproveProfileAsync(profile.Id);
                
                if (result.Success)
                {
                    profile.IsApproved = true;
                    AppLogger.LogInfo($"Cloud profile approved: {profile.Name}");
                }
                else
                {
                    AppLogger.LogWarn($"Failed to approve cloud profile {profile.Name}: {result.Message}");
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"Error approving cloud profile: {profile.Name}", ex);
            }
        }

        private async Task ValidateProfileTrust(CloudProfile profile)
        {
            try
            {
                // Check if publisher is in trusted list
                var trustedPublishers = await _signatureValidationService.GetTrustedPublishersAsync();
                var isTrustedPublisher = trustedPublishers.Any(tp => 
                    string.Equals(tp.Name, profile.PublisherName, StringComparison.OrdinalIgnoreCase) ||
                    tp.Fingerprints.Contains(profile.SignatureFingerprint, StringComparer.OrdinalIgnoreCase));

                profile.IsTrustedPublisher = isTrustedPublisher;
                
                // Set trust level and icon
                if (isTrustedPublisher && profile.IsVerified)
                {
                    profile.TrustLevel = "Verified Publisher";
                    profile.TrustIcon = "🔐";
                }
                else if (profile.IsVerified)
                {
                    profile.TrustLevel = "Verified";
                    profile.TrustIcon = "✓";
                }
                else
                {
                    profile.TrustLevel = "Community";
                    profile.TrustIcon = "⚠";
                    profile.HasSignatureWarning = true;
                }

                // Additional enterprise security checks
                if (profile.RequiresElevatedPrivileges && !isTrustedPublisher)
                {
                    profile.HasSignatureWarning = true;
                    SecurityWarningRaised?.Invoke(this, new SecurityWarningEventArgs(
                        profile, 
                        "This profile requires elevated privileges but is not from a trusted publisher."
                    ));
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"Error validating trust for profile: {profile.Name}", ex);
                profile.HasSignatureWarning = true;
            }
        }

        public void ApplyFilters()
        {
            try
            {
                var filtered = _cloudProfiles.AsEnumerable();

                // Search filter
                if (!string.IsNullOrWhiteSpace(SearchQuery))
                {
                    filtered = filtered.Where(p => 
                        p.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                        p.Description.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                        p.PublisherName.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));
                }

                // Category filter
                if (!string.IsNullOrWhiteSpace(SelectedCategory) && SelectedCategory != "All Categories")
                {
                    filtered = filtered.Where(p => p.Category == SelectedCategory);
                }

                // Trust level filter
                if (!string.IsNullOrWhiteSpace(SelectedTrustLevel) && SelectedTrustLevel != "All Trust Levels")
                {
                    filtered = SelectedTrustLevel switch
                    {
                        "Verified Publisher" => filtered.Where(p => p.IsTrustedPublisher && p.IsVerified),
                        "Community" => filtered.Where(p => !p.IsTrustedPublisher),
                        "Enterprise Only" => filtered.Where(p => p.IsEnterpriseOnly),
                        _ => filtered
                    };
                }

                // Approved only filter
                if (ShowOnlyApproved)
                {
                    filtered = filtered.Where(p => p.IsApproved);
                }

                // Sort
                filtered = SortBy switch
                {
                    "Most Popular" => filtered.OrderByDescending(p => p.DownloadCount),
                    "Recently Updated" => filtered.OrderByDescending(p => p.LastUpdated),
                    "Alphabetical" => filtered.OrderBy(p => p.Name),
                    "Trust Score" => filtered.OrderByDescending(p => p.IsVerified ? (p.IsTrustedPublisher ? 3 : 2) : 1),
                    _ => filtered.OrderByDescending(p => p.DownloadCount)
                };

                CloudProfiles.Clear();
                foreach (var profile in filtered)
                {
                    CloudProfiles.Add(profile);
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Error applying filters to cloud profiles", ex);
            }
        }

        public void AddToRecentDownloads(CloudProfile profile)
        {
            // Implementation for tracking recent downloads
            AppLogger.LogInfo($"Added to recent downloads: {profile.Name}");
        }

        #endregion

        #region Events

        public event EventHandler<CloudProfileEventArgs>? ProfileDownloaded;
        public event EventHandler<SecurityWarningEventArgs>? SecurityWarningRaised;
        public event EventHandler<TrustVerificationEventArgs>? TrustVerificationCompleted;
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _cloudProfileService?.Dispose();
        }

        private CloudProfile ConvertToCloudProfile(CommunityProfile communityProfile)
        {
            return new CloudProfile
            {
                Id = communityProfile.Id,
                Name = communityProfile.Name,
                Description = communityProfile.Description,
                Category = communityProfile.Category,
                Version = "1.0", // Default version
                PublisherName = communityProfile.Author,
                LastUpdated = communityProfile.LastUpdated,
                Rating = (double)communityProfile.Rating,
                DownloadCount = communityProfile.Downloads,
                IsVerified = communityProfile.AuthorVerified,
                IsTrustedPublisher = false, // Default value
                IsApproved = false, // Default value
                IsEnterpriseOnly = false, // Default value
                HasSignatureWarning = false, // Default value
                RequiresElevatedPrivileges = false, // Default value
                TrustLevel = "Community",
                TrustIcon = "⚠"
            };
        }

        #endregion
    }

    // Supporting classes
    public class CloudProfile
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string PublisherName { get; set; } = string.Empty;
        public string PublisherInitials => GetInitials(PublisherName);
        public DateTime LastUpdated { get; set; }
        public double Rating { get; set; }
        public int DownloadCount { get; set; }
        public bool IsVerified { get; set; }
        public bool IsTrustedPublisher { get; set; }
        public bool IsApproved { get; set; }
        public bool IsEnterpriseOnly { get; set; }
        public bool HasSignatureWarning { get; set; }
        public bool RequiresElevatedPrivileges { get; set; }
        public string TrustLevel { get; set; } = "Community";
        public string TrustIcon { get; set; } = "⚠";
        public string SignatureFingerprint { get; set; } = string.Empty;

        private string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "??";
            var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return words.Length switch
            {
                1 => words[0].Substring(0, Math.Min(2, words[0].Length)).ToUpper(),
                _ => (words[0][0].ToString() + words[^1][0].ToString()).ToUpper()
            };
        }
    }

    // Event argument classes
    public class CloudProfileEventArgs : EventArgs
    {
        public CloudProfile Profile { get; }
        public CloudProfileEventArgs(CloudProfile profile) => Profile = profile;
    }

    public class SecurityWarningEventArgs : EventArgs
    {
        public CloudProfile Profile { get; }
        public string Warning { get; }
        public bool ContinueAnyway { get; set; }
        
        public SecurityWarningEventArgs(CloudProfile profile, string warning)
        {
            Profile = profile;
            Warning = warning;
        }
    }

    public class TrustVerificationEventArgs : EventArgs
    {
        public CloudProfile Profile { get; }
        public bool IsVerified { get; }
        public string? ErrorMessage { get; }
        
        public TrustVerificationEventArgs(CloudProfile profile, bool isVerified, string? errorMessage = null)
        {
            Profile = profile;
            IsVerified = isVerified;
            ErrorMessage = errorMessage;
        }
    }

    // Simple relay command implementation
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool>? _canExecute;

        public RelayCommand(Action<T> execute, Func<T, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter)
        {
            return _canExecute == null || _canExecute((T)parameter!);
        }

        public void Execute(object? parameter)
        {
            _execute((T)parameter!);
        }
    }
}
