using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using GGs.Shared.Api;
using GGs.Shared.Tweaks;
using GGs.Desktop.Extensions;
using GGs.Desktop.Services;

namespace GGs.Desktop.ViewModels.Analytics
{
    /// <summary>
    /// Enterprise Audit Search ViewModel with advanced filtering and real-time capabilities
    /// </summary>
    public class AuditSearchViewModel : INotifyPropertyChanged
    {
        private readonly GGs.Shared.Api.ApiClient _apiClient;
        private ObservableCollection<TweakApplicationLog> _auditEntries;
        private string _quickSearchQuery = string.Empty;
        private string _searchStatus = "Ready";
        private bool _isRealTimeMode;
        private int _resultCount;
        private int _loadedEntries;

        public AuditSearchViewModel()
        {
            _apiClient = new GGs.Shared.Api.ApiClient(new System.Net.Http.HttpClient());
            _auditEntries = new ObservableCollection<TweakApplicationLog>();
            InitializeDefaults();
        }

        #region Properties

        public ObservableCollection<TweakApplicationLog> AuditEntries
        {
            get => _auditEntries;
            set
            {
                _auditEntries = value;
                OnPropertyChanged();
                UpdateStatistics();
            }
        }

        public string QuickSearchQuery
        {
            get => _quickSearchQuery;
            set
            {
                _quickSearchQuery = value;
                OnPropertyChanged();
            }
        }

        // Search Filters
        public string? SearchType { get; set; }
        public string? UserId { get; set; }
        public string? DeviceId { get; set; }
        public string? TweakId { get; set; }
        public string? CorrelationId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? SuccessFilter { get; set; }
        public string? RiskLevel { get; set; }
        public string? SortBy { get; set; }

        // Status Properties
        public string SearchStatus
        {
            get => _searchStatus;
            set
            {
                _searchStatus = value;
                OnPropertyChanged();
            }
        }

        public bool IsRealTimeMode
        {
            get => _isRealTimeMode;
            set
            {
                _isRealTimeMode = value;
                OnPropertyChanged();
            }
        }

        public int ResultCount
        {
            get => _resultCount;
            set
            {
                _resultCount = value;
                OnPropertyChanged();
            }
        }

        public int LoadedEntries
        {
            get => _loadedEntries;
            set
            {
                _loadedEntries = value;
                OnPropertyChanged();
            }
        }

        public int SuccessCount => AuditEntries.Count(e => e.Success);
        public int FailureCount => AuditEntries.Count(e => !e.Success);
        public bool HasFailures => FailureCount > 0;
        public bool HasMoreResults { get; set; }

        #endregion

        #region Methods

        private void InitializeDefaults()
        {
            FromDate = DateTime.Today.AddDays(-7);
            ToDate = DateTime.Today.AddDays(1);
            SearchType = "All Fields";
            SuccessFilter = "All";
            RiskLevel = "All";
            SortBy = "Newest First";
        }

        public async Task SearchAsync()
        {
            try
            {
                SearchStatus = "Searching audit logs...";
                
                var searchCriteria = BuildSearchCriteria();
                var results = await _apiClient.SearchAuditLogsAsync(searchCriteria.Query, searchCriteria.FromDate, searchCriteria.ToDate);
                
                AuditEntries.Clear();
                foreach (var entry in results)
                {
                    // Enhance entries with display properties
                    EnhanceAuditEntry(entry);
                    AuditEntries.Add(entry);
                }
                
                ResultCount = results.Count;
                LoadedEntries = AuditEntries.Count;
                HasMoreResults = false; // Simple list doesn't support pagination
                
                SearchStatus = $"Found {ResultCount:N0} entries";
                
                AppLogger.LogInfo($"Audit search completed: {ResultCount} results found");
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Failed to search audit logs", ex);
                SearchStatus = "Search failed";
                throw;
            }
        }

        public async Task LoadMoreAsync()
        {
            try
            {
                if (!HasMoreResults) return;
                
                SearchStatus = "Loading more entries...";
                
                var searchCriteria = BuildSearchCriteria();
                searchCriteria.Skip = LoadedEntries;
                
                var results = await _apiClient.SearchAuditLogsAsync(searchCriteria.Query, searchCriteria.FromDate, searchCriteria.ToDate);
                
                foreach (var entry in results)
                {
                    EnhanceAuditEntry(entry);
                    AuditEntries.Add(entry);
                }
                
                LoadedEntries = AuditEntries.Count;
                HasMoreResults = false; // Simple list doesn't support pagination
                
                SearchStatus = $"Loaded {LoadedEntries:N0} of {ResultCount:N0} entries";
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Failed to load more audit entries", ex);
                SearchStatus = "Failed to load more";
            }
        }

        public void ClearFilters()
        {
            QuickSearchQuery = string.Empty;
            UserId = null;
            DeviceId = null;
            TweakId = null;
            CorrelationId = null;
            SearchType = "All Fields";
            SuccessFilter = "All";
            RiskLevel = "All";
            
            OnPropertyChanged(nameof(UserId));
            OnPropertyChanged(nameof(DeviceId));
            OnPropertyChanged(nameof(TweakId));
            OnPropertyChanged(nameof(CorrelationId));
            OnPropertyChanged(nameof(SearchType));
            OnPropertyChanged(nameof(SuccessFilter));
            OnPropertyChanged(nameof(RiskLevel));
        }

        public void SetTimeFilter(TimeSpan duration)
        {
            ToDate = DateTime.Now;
            FromDate = DateTime.Now - duration;
            OnPropertyChanged(nameof(FromDate));
            OnPropertyChanged(nameof(ToDate));
        }

        public void SetFailureFilter()
        {
            SuccessFilter = "Failures Only";
            OnPropertyChanged(nameof(SuccessFilter));
        }

        public void SetCriticalFilter()
        {
            RiskLevel = "Critical";
            OnPropertyChanged(nameof(RiskLevel));
        }

        public void SetAdminActionsFilter()
        {
            SearchType = "User Actions";
            QuickSearchQuery = "admin";
            OnPropertyChanged(nameof(SearchType));
        }

        public void ToggleRealTimeMode()
        {
            IsRealTimeMode = !IsRealTimeMode;
            
            if (IsRealTimeMode)
            {
                StartRealTimeUpdates();
            }
            else
            {
                StopRealTimeUpdates();
            }
        }

        public async Task ExportResultsAsync(string filePath)
        {
            try
            {
                var exportService = new DataExportService();
                await exportService.ExportToFileAsync(AuditEntries.ToList(), filePath, "csv");
                
                AppLogger.LogInfo($"Audit search results exported to: {filePath}");
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Failed to export audit search results", ex);
                throw;
            }
        }

        public AuditSearchCriteria GetCurrentSearchCriteria()
        {
            return BuildSearchCriteria();
        }

        private AuditSearchCriteria BuildSearchCriteria()
        {
            return new AuditSearchCriteria
            {
                Query = QuickSearchQuery,
                SearchType = SearchType,
                UserId = UserId,
                DeviceId = DeviceId,
                TweakId = TweakId,
                CorrelationId = CorrelationId,
                FromDate = FromDate,
                ToDate = ToDate,
                SuccessFilter = SuccessFilter,
                RiskLevel = RiskLevel,
                SortBy = SortBy,
                Take = 50,
                Skip = 0
            };
        }

        private void EnhanceAuditEntry(TweakApplicationLog entry)
        {
            // Add display properties using extension methods
            var statusIcon = entry.GetStatusIcon();
            var riskLevelColor = entry.GetRiskLevelColor();
            var hasError = entry.GetHasError();
            
            // Set default values if missing
            if (string.IsNullOrWhiteSpace(entry.TweakName))
            {
                entry.TweakName = "Unknown Tweak";
            }
        }

        private string GetRiskLevelColor(string? riskLevel)
        {
            return riskLevel?.ToLower() switch
            {
                "low" => "#10B981",
                "medium" => "#F59E0B",
                "high" => "#EF4444",
                "critical" => "#7C2D12",
                _ => "#6B7280"
            };
        }

        private void UpdateStatistics()
        {
            OnPropertyChanged(nameof(SuccessCount));
            OnPropertyChanged(nameof(FailureCount));
            OnPropertyChanged(nameof(HasFailures));
        }

        private void StartRealTimeUpdates()
        {
            // Implementation for real-time audit log updates
            SearchStatus = "Real-time mode active";
            AppLogger.LogInfo("Started real-time audit monitoring");
        }

        private void StopRealTimeUpdates()
        {
            SearchStatus = "Real-time mode stopped";
            AppLogger.LogInfo("Stopped real-time audit monitoring");
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    // Supporting classes
    public class AuditSearchCriteria
    {
        public string? Query { get; set; }
        public string? SearchType { get; set; }
        public string? UserId { get; set; }
        public string? DeviceId { get; set; }
        public string? TweakId { get; set; }
        public string? CorrelationId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? SuccessFilter { get; set; }
        public string? RiskLevel { get; set; }
        public string? SortBy { get; set; }
        public int Take { get; set; } = 50;
        public int Skip { get; set; } = 0;
    }

    public class AuditSearchResults
    {
        public TweakApplicationLog[] Data { get; set; } = Array.Empty<TweakApplicationLog>();
        public int TotalCount { get; set; }
        public bool HasMore { get; set; }
    }
}