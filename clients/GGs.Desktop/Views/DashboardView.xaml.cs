using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GGs.Desktop.Services;
using GGs.Desktop.ViewModels;
using GGs.Shared.Api;
using GGs.Shared.Enums;
using Microsoft.Extensions.Logging;
using GGs.Desktop.Views.Controls;

namespace GGs.Desktop.Views
{
    public partial class DashboardView : UserControl
    {
        private readonly DashboardViewModel _viewModel;
        private readonly EliBotService _eliBotService;
        // Removed non-applicable instance field for static EntitlementsService
        private readonly SystemMonitorService _systemMonitor;
        private readonly ILogger<DashboardView> _logger;

        public ObservableCollection<ChatMessageViewModel> ChatMessages { get; } = new();

        public DashboardView(
            DashboardViewModel viewModel,
            EliBotService eliBotService,
            SystemMonitorService systemMonitor,
            ILogger<DashboardView> logger)
        {
            InitializeComponent();
            
            _viewModel = viewModel;
            _eliBotService = eliBotService;
            _systemMonitor = systemMonitor;
            _logger = logger;

            DataContext = _viewModel;
            ChatHistory.ItemsSource = ChatMessages;

            Loaded += DashboardView_Loaded;
        }

        private async void DashboardView_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await InitializeDashboardAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize dashboard");
                ShowErrorMessage("Failed to load dashboard data. Please try refreshing.");
            }
        }

        private async Task InitializeDashboardAsync()
        {
            // Load user entitlements and customize UI
            var entitlements = await GetUserEntitlementsAsync();
            UpdateUIBasedOnEntitlements(entitlements);

            // Load initial chat message from EliBot
            await AddEliBotWelcomeMessage(entitlements);

            // Load system statistics
            await LoadSystemStatisticsAsync();

            // Load recent activity
            await LoadRecentActivityAsync();

            // Initialize performance monitoring
            await InitializePerformanceMonitoringAsync();
        }

        private void UpdateUIBasedOnEntitlements(EntitlementsResponse entitlements)
        {
            // Update welcome message based on user tier/role
            WelcomeText.Text = $"Welcome back, {GetUserDisplayName(entitlements)}!";
            
            // Show EliBot question limit
            var dailyLimit = entitlements.Entitlements.EliBot.DailyQuestionLimit;
            if (dailyLimit < int.MaxValue)
            {
                SetChatPlaceholder($"Ask EliBot anything... ({dailyLimit} questions remaining today)");
            }

            // Hide features not available to user tier
            if (!entitlements.Entitlements.Monitoring.RealTimeCharts)
            {
                PerformanceChart.Visibility = Visibility.Collapsed;
            }

            if (!entitlements.Entitlements.Tweaks.CustomTweakCreation)
            {
                // Hide create tweak quick action
                var createTweakButton = FindName("CreateTweakQuickAction") as FrameworkElement;
                if (createTweakButton != null)
                    createTweakButton.Visibility = Visibility.Collapsed;
            }
        }

        private async Task AddEliBotWelcomeMessage(EntitlementsResponse entitlements)
        {
            try
            {
                var welcomePrompt = GeneratePersonalizedWelcomePrompt(entitlements);
                var response = await _eliBotService.AskQuestionAsync(welcomePrompt);

                var welcomeMessage = new ChatMessageViewModel
                {
                    Content = response.Answer,
                    IsFromBot = true,
                    Timestamp = DateTime.Now,
                    IsPersonalized = true
                };

                ChatMessages.Add(welcomeMessage);
                ScrollChatToBottom();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load EliBot welcome message");
            }
        }

        private string GeneratePersonalizedWelcomePrompt(EntitlementsResponse entitlements)
        {
            var tier = entitlements.Entitlements.RoleName;
            var hasAdvancedFeatures = entitlements.Entitlements.EliBot.SystemDiagnostics;
            
            return $"Generate a personalized welcome message for a {tier} tier user. " +
                   $"Highlight 2-3 key features available to them. " +
                   $"Advanced features available: {hasAdvancedFeatures}. " +
                   "Keep it friendly, professional, and under 100 words.";
        }

        private async Task LoadSystemStatisticsAsync()
        {
            try
            {
                var stats = await _systemMonitor.GetSystemStatisticsAsync();
                
                // Update stat cards with real data
                UpdateStatCard("ActiveUsersCard", stats.ActiveUsers.ToString("N0"), 
                              CalculatePercentageChange(stats.ActiveUsers, stats.PreviousActiveUsers));
                
                UpdateStatCard("TweaksAppliedCard", stats.TweaksApplied.ToString("N0"),
                              CalculatePercentageChange(stats.TweaksApplied, stats.PreviousTweaksApplied));
                
                UpdateStatCard("PerformanceGainCard", $"{stats.AveragePerformanceGain:P1}",
                              CalculatePercentageChange(stats.AveragePerformanceGain, stats.PreviousPerformanceGain));
                
                UpdateStatCard("EliBotQueriesCard", stats.EliBotQueries.ToString("N0"),
                              CalculatePercentageChange(stats.EliBotQueries, stats.PreviousEliBotQueries));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load system statistics");
            }
        }

        private void UpdateStatCard(string cardName, string value, double changePercent)
        {
            if (FindName(cardName) is StatCard card)
            {
                card.Value = value;
                card.Change = $"{changePercent:+0.0;-0.0}%";
                card.IsPositive = changePercent >= 0;
            }
        }

        private double CalculatePercentageChange(double current, double previous)
        {
            if (previous == 0) return 0;
            return ((current - previous) / previous) * 100;
        }

        private async Task LoadRecentActivityAsync()
        {
            try
            {
                var activities = await _systemMonitor.GetRecentActivitiesAsync(10);
                ActivityFeed.LoadActivities(activities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load recent activities");
            }
        }

        private async Task InitializePerformanceMonitoringAsync()
        {
            try
            {
                var entitlements = await GetUserEntitlementsAsync();
                if (entitlements.Entitlements.Monitoring.RealTimeCharts)
                {
                    await PerformanceChart.InitializeWithRealtimeDataAsync();
                }
                else
                {
                    await PerformanceChart.InitializeWithStaticDataAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize performance monitoring");
            }
        }

        private async void SendChat_Click(object sender, RoutedEventArgs e)
        {
            await SendChatMessageAsync();
        }

        private async void ChatInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(ChatInput.Text))
            {
                await SendChatMessageAsync();
            }
        }

        private async Task SendChatMessageAsync()
        {
            var question = ChatInput.Text.Trim();
            if (string.IsNullOrEmpty(question)) return;

            // Clear input immediately for better UX
            ChatInput.Text = string.Empty;

            // Add user message
            var userMessage = new ChatMessageViewModel
            {
                Content = question,
                IsFromBot = false,
                Timestamp = DateTime.Now
            };
            ChatMessages.Add(userMessage);

            // Add typing indicator
            var typingMessage = new ChatMessageViewModel
            {
                Content = "EliBot is thinking...",
                IsFromBot = true,
                Timestamp = DateTime.Now,
                IsTyping = true
            };
            ChatMessages.Add(typingMessage);
            ScrollChatToBottom();

            try
            {
                // Check rate limits
                var entitlements = await GetUserEntitlementsAsync();
                var canAsk = await _eliBotService.CanAskQuestionAsync();
                
                if (!canAsk)
                {
                    ChatMessages.Remove(typingMessage);
                    var limitMessage = new ChatMessageViewModel
                    {
                        Content = $"You've reached your daily limit of {entitlements.Entitlements.EliBot.DailyQuestionLimit} questions. Upgrade your plan for unlimited access!",
                        IsFromBot = true,
                        Timestamp = DateTime.Now,
                        IsError = true
                    };
                    ChatMessages.Add(limitMessage);
                    return;
                }

                // Get AI response
                var response = await _eliBotService.AskQuestionAsync(question);
                
                // Remove typing indicator and add response
                ChatMessages.Remove(typingMessage);
                
                var botMessage = new ChatMessageViewModel
                {
                    Content = response.Answer,
                    IsFromBot = true,
                    Timestamp = DateTime.Now,
                    TokensUsed = response.TokensUsed,
                    Cost = response.Cost
                };
                ChatMessages.Add(botMessage);
                
                // Update UI with remaining questions
                await UpdateRemainingQuestionsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get EliBot response for question: {Question}", question);
                
                ChatMessages.Remove(typingMessage);
                var errorMessage = new ChatMessageViewModel
                {
                    Content = "Sorry, I'm having trouble right now. Please try again in a moment.",
                    IsFromBot = true,
                    Timestamp = DateTime.Now,
                    IsError = true
                };
                ChatMessages.Add(errorMessage);
            }
            finally
            {
                ScrollChatToBottom();
            }
        }

        private async Task UpdateRemainingQuestionsAsync()
        {
            try
            {
                var usage = await _eliBotService.GetDailyUsageAsync();
                var entitlements = await GetUserEntitlementsAsync();
                var remaining = Math.Max(0, entitlements.Entitlements.EliBot.DailyQuestionLimit - usage.QuestionsUsedToday);
                
                if (remaining < entitlements.Entitlements.EliBot.DailyQuestionLimit)
                {
                    SetChatPlaceholder($"Ask EliBot anything... ({remaining} questions remaining today)");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update remaining questions count");
            }
        }

        private void SetChatPlaceholder(string text)
        {
            // WPF TextBox doesn't have a built-in PlaceholderText. Use Tag as a fallback for styles that display it.
            ChatInput.Tag = text;
            ChatInput.ToolTip = text; // secondary hint
        }

        private string GetUserDisplayName(EntitlementsResponse entitlements)
        {
            // Extract user name from entitlements or use role-based display name
            return entitlements.Entitlements.Role switch
            {
                RbacRole.Owner => "Owner",
                RbacRole.Admin => "Administrator",
                RbacRole.Moderator => "Moderator",
                RbacRole.Enterprise => "Enterprise User",
                RbacRole.Pro => "Pro User",
                RbacRole.Basic => "User",
                _ => "User"
            };
        }

        // Helper to obtain EntitlementsResponse from desktop static service/state.
        private Task<EntitlementsResponse> GetUserEntitlementsAsync()
        {
            // Build entitlements based on current application state
            // In production, this would integrate with license validation service
            var ent = new Entitlements
            {
                Role = RbacRole.Pro,
                RoleName = "Pro",
                EliBot = new EliBotQuota 
                { 
                    DailyQuestionLimit = int.MaxValue, 
                    PredictiveOptimization = true, 
                    SystemDiagnostics = true 
                },
                Monitoring = new MonitoringCapabilities 
                { 
                    RealTimeCharts = true, 
                    HistoryDays = 30, 
                    CustomMetrics = true, 
                    TeamDashboards = false 
                },
                Tweaks = new TweakCapabilities 
                { 
                    AllowLowRisk = true, 
                    AllowMediumRisk = true, 
                    AllowHighRisk = false, 
                    AllowExperimental = false, 
                    CustomTweakCreation = true, 
                    TeamSharing = false, 
                    ApprovalWorkflows = false 
                }
            };
            return Task.FromResult(new EntitlementsResponse { Entitlements = ent });
        }

        private void ShowErrorMessage(string text)
        {
            // Display error message with visual feedback
            SystemStatusText.Text = text;
            SystemStatusText.Foreground = System.Windows.Media.Brushes.IndianRed;
            
            // Auto-clear after 5 seconds
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += (s, e) =>
            {
                SystemStatusText.Text = string.Empty;
                timer.Stop();
            };
            timer.Start();
        }

        private void ScrollChatToBottom()
        {
            ChatScrollViewer.ScrollToEnd();
        }

        private void QuickOptimize_Click(object sender, RoutedEventArgs e)
        {
            // Hooked up in XAML; you may connect to view model if desired.
            _ = _systemMonitor.RunQuickOptimizationAsync();
        }

        private async void SystemScan_Click(object sender, RoutedEventArgs e)
        {
            var results = await _systemMonitor.RunSystemScanAsync();
            // For now, display simple status
            ShowErrorMessage($"Scan complete. Issues found: {results.IssuesFound}");
        }

        private void PerformanceTimeRange_Changed(object sender, SelectionChangedEventArgs e)
        {
            // Refresh performance chart data based on selected time range
            if (PerformanceTimeRange?.SelectedItem is ComboBoxItem selectedItem)
            {
                var tag = selectedItem.Tag?.ToString();
                if (!string.IsNullOrEmpty(tag))
                {
                    // Trigger chart refresh with new time range
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            // Refresh system metrics with new time window
                            await Task.Delay(100); // Allow UI to update
                            Dispatcher.Invoke(() =>
                            {
                                // Update chart time range (actual chart update would happen here)
                                SystemStatusText.Text = $"Performance range: {tag}";
                            });
                        }
                        catch (Exception ex)
                        {
                            Dispatcher.Invoke(() => ShowErrorMessage($"Failed to refresh performance data: {ex.Message}"));
                        }
                    });
                }
            }
        }
    }

    public class ChatMessageViewModel
    {
        public string Content { get; set; } = string.Empty;
        public bool IsFromBot { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsTyping { get; set; }
        public bool IsError { get; set; }
        public bool IsPersonalized { get; set; }
        public int? TokensUsed { get; set; }
        public decimal? Cost { get; set; }
    }

    public enum NotificationType
    {
        Success,
        Error,
        Warning,
        Info
    }
}
