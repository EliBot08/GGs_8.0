using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using GGs.Desktop.Services;
using GGs.Desktop.ViewModels;
using Microsoft.Extensions.Logging;

namespace GGs.Desktop.Views
{
    public partial class OnboardingWizard : Window
    {
        private readonly OnboardingViewModel _viewModel;
        private readonly FirstRunService _firstRunService;
        private readonly EntitlementsService _entitlementsService;
        private readonly EliBotService _eliBotService;
        private readonly SystemMonitorService _systemMonitor;
        private readonly ILogger<OnboardingWizard> _logger;
        private readonly IAccessibilityService _accessibilityService;

        private int _currentStep = 1;
        private const int TotalSteps = 4;

        public OnboardingWizard(
            OnboardingViewModel viewModel,
            FirstRunService firstRunService,
            EntitlementsService entitlementsService,
            EliBotService eliBotService,
            SystemMonitorService systemMonitor,
            ILogger<OnboardingWizard> logger,
            IAccessibilityService accessibilityService)
        {
            InitializeComponent();
            
            _viewModel = viewModel;
            _firstRunService = firstRunService;
            _entitlementsService = entitlementsService;
            _eliBotService = eliBotService;
            _systemMonitor = systemMonitor;
            _logger = logger;
            _accessibilityService = accessibilityService;

            DataContext = _viewModel;
            
            // Configure for accessibility
            _accessibilityService.ConfigureForAccessibility(this);
            _accessibilityService.SetAccessibilityProperties(this, "GGs Onboarding Wizard", 
                "Welcome wizard to set up GGs Enterprise features");

            Loaded += OnboardingWizard_Loaded;
        }

        private async void OnboardingWizard_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Animate window entrance
                var fadeIn = FindResource("FadeInAnimation") as Storyboard;
                fadeIn?.Begin(this);

                // Load user entitlements to customize experience
                await LoadUserEntitlementsAsync();
                
                // Set up initial step
                UpdateStepUI();
                
                _accessibilityService.AnnounceToScreenReader("GGs onboarding wizard opened. Welcome screen displayed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize onboarding wizard");
                ShowError("Failed to initialize onboarding. Please try again.");
            }
        }

        private async Task LoadUserEntitlementsAsync()
        {
            try
            {
                var entitlements = await _entitlementsService.GetUserEntitlementsAsync();
                _viewModel.UserEntitlements = entitlements;
                
                // Customize welcome message based on tier
                var tierMessage = entitlements.LicenseTier switch
                {
                    LicenseTier.Owner => "Welcome, Owner! You have access to all enterprise features.",
                    LicenseTier.Admin => "Welcome, Administrator! Full management capabilities available.",
                    LicenseTier.Moderator => "Welcome, Moderator! User support and content moderation tools ready.",
                    LicenseTier.Enterprise => "Welcome to GGs Enterprise! Advanced features unlocked.",
                    LicenseTier.Pro => "Welcome to GGs Pro! Enhanced gaming optimization awaits.",
                    LicenseTier.Basic => "Welcome to GGs! Let's optimize your gaming experience.",
                    _ => "Welcome to GGs!"
                };

                WelcomeSubtitle.Text = tierMessage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load user entitlements");
            }
        }

        private void UpdateStepUI()
        {
            // Update progress dots
            UpdateProgressDots();
            
            // Update step info
            var (title, description) = GetStepInfo(_currentStep);
            StepTitle.Text = title;
            StepDescription.Text = $"Step {_currentStep} of {TotalSteps}";

            // Update navigation buttons
            BackButton.IsEnabled = _currentStep > 1;
            NextButton.Content = _currentStep == TotalSteps ? "Finish" : "Next";
            
            // Load step content
            LoadStepContent(_currentStep);
            
            _accessibilityService.AnnounceToScreenReader($"Step {_currentStep}: {title}");
        }

        private void UpdateProgressDots()
        {
            var dots = new[] { Step1Dot, Step2Dot, Step3Dot, Step4Dot };
            
            for (int i = 0; i < dots.Length; i++)
            {
                var dot = dots[i];
                dot.IsActive = (i + 1) == _currentStep;
                dot.IsCompleted = (i + 1) < _currentStep;
            }
        }

        private (string title, string description) GetStepInfo(int step)
        {
            return step switch
            {
                1 => ("Welcome", "Introduction to GGs Enterprise"),
                2 => ("System Profile", "Configure your gaming setup"),
                3 => ("Optimization Preferences", "Set your performance goals"),
                4 => ("Complete Setup", "Test features and finish"),
                _ => ("Unknown", "")
            };
        }

        private void LoadStepContent(int step)
        {
            UserControl content = step switch
            {
                1 => new WelcomeStepControl(_viewModel),
                2 => new SystemProfileStepControl(_viewModel, _systemMonitor),
                3 => new OptimizationPreferencesStepControl(_viewModel),
                4 => new CompleteSetupStepControl(_viewModel, _eliBotService),
                _ => throw new ArgumentOutOfRangeException(nameof(step))
            };

            // Animate content change
            AnimateContentChange(content);
        }

        private void AnimateContentChange(UserControl newContent)
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
            fadeOut.Completed += (s, e) =>
            {
                ContentPresenter.Content = newContent;
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
                ContentPresenter.BeginAnimation(OpacityProperty, fadeIn);
            };
            
            ContentPresenter.BeginAnimation(OpacityProperty, fadeOut);
        }

        private async void Next_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate current step
                if (!await ValidateCurrentStep())
                    return;

                if (_currentStep == TotalSteps)
                {
                    // Finish onboarding
                    await FinishOnboardingAsync();
                    return;
                }

                _currentStep++;
                UpdateStepUI();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error navigating to next step");
                ShowError("Failed to proceed to next step. Please try again.");
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep > 1)
            {
                _currentStep--;
                UpdateStepUI();
            }
        }

        private async void Skip_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to skip the setup wizard? You can access these settings later from the Settings menu.",
                "Skip Setup",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                await FinishOnboardingAsync(skipSetup: true);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to close the setup wizard? Your gaming experience may not be optimized.",
                "Close Setup",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                DialogResult = false;
                Close();
            }
        }

        private async Task<bool> ValidateCurrentStep()
        {
            switch (_currentStep)
            {
                case 1:
                    return true; // Welcome step always valid

                case 2:
                    // Validate system profile
                    if (string.IsNullOrEmpty(_viewModel.SelectedGameProfile))
                    {
                        ShowError("Please select a gaming profile to continue.");
                        return false;
                    }
                    break;

                case 3:
                    // Validate optimization preferences
                    if (!_viewModel.HasSelectedOptimizations)
                    {
                        ShowError("Please select at least one optimization preference.");
                        return false;
                    }
                    break;

                case 4:
                    // Test EliBot if user wants to
                    if (_viewModel.TestEliBot && !string.IsNullOrEmpty(_viewModel.TestQuestion))
                    {
                        await TestEliBotAsync();
                    }
                    break;
            }

            return true;
        }

        private async Task TestEliBotAsync()
        {
            try
            {
                _viewModel.IsTestingEliBot = true;
                var response = await _eliBotService.AskQuestionAsync(_viewModel.TestQuestion);
                _viewModel.EliBotTestResponse = response.Answer;
                
                _accessibilityService.AnnounceToScreenReader($"EliBot responded: {response.Answer}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to test EliBot");
                _viewModel.EliBotTestResponse = "Sorry, EliBot is currently unavailable. Please try again later.";
            }
            finally
            {
                _viewModel.IsTestingEliBot = false;
            }
        }

        private async Task FinishOnboardingAsync(bool skipSetup = false)
        {
            try
            {
                if (!skipSetup)
                {
                    // Apply selected configurations
                    await ApplyConfigurationsAsync();
                }

                // Mark first run as complete
                await _firstRunService.CompleteFirstRunAsync();

                // Show completion message
                _accessibilityService.AnnounceToScreenReader("Onboarding completed successfully!");
                
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to complete onboarding");
                ShowError("Failed to complete setup. Please try again or contact support.");
            }
        }

        private async Task ApplyConfigurationsAsync()
        {
            try
            {
                // Apply gaming profile
                if (!string.IsNullOrEmpty(_viewModel.SelectedGameProfile))
                {
                    await _systemMonitor.ApplyGamingProfileAsync(_viewModel.SelectedGameProfile);
                }

                // Apply optimization preferences
                if (_viewModel.HasSelectedOptimizations)
                {
                    await ApplyOptimizationPreferencesAsync();
                }

                // Save user preferences
                await _firstRunService.SaveUserPreferencesAsync(_viewModel.GetUserPreferences());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply configurations");
                throw;
            }
        }

        private async Task ApplyOptimizationPreferencesAsync()
        {
            var optimizations = _viewModel.GetSelectedOptimizations();
            foreach (var optimization in optimizations)
            {
                try
                {
                    await _systemMonitor.ApplyOptimizationAsync(optimization);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to apply optimization: {Optimization}", optimization);
                }
            }
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message, "Setup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            _accessibilityService.AnnounceToScreenReader($"Error: {message}");
        }

        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            // Handle keyboard shortcuts
            switch (e.Key)
            {
                case System.Windows.Input.Key.Enter:
                    if (NextButton.IsEnabled)
                    {
                        Next_Click(NextButton, new RoutedEventArgs());
                        e.Handled = true;
                    }
                    break;

                case System.Windows.Input.Key.Escape:
                    Close_Click(CloseButton, new RoutedEventArgs());
                    e.Handled = true;
                    break;

                case System.Windows.Input.Key.F1:
                    ShowHelp();
                    e.Handled = true;
                    break;
            }

            base.OnKeyDown(e);
        }

        private void ShowHelp()
        {
            var helpText = _currentStep switch
            {
                1 => "This wizard will help you set up GGs for optimal gaming performance. Use Tab to navigate, Enter to proceed.",
                2 => "Select your primary gaming style to optimize system settings. Choose the profile that best matches your needs.",
                3 => "Choose optimization preferences based on your priorities. You can change these later in settings.",
                4 => "Test EliBot AI assistant and complete your setup. All configurations will be applied automatically.",
                _ => "Use the navigation buttons to move through the setup wizard."
            };

            MessageBox.Show(helpText, "Help", MessageBoxButton.OK, MessageBoxImage.Information);
            _accessibilityService.AnnounceToScreenReader($"Help: {helpText}");
        }
    }
}

// Step control base class
public abstract class OnboardingStepControl : UserControl
{
    protected OnboardingViewModel ViewModel { get; }

    protected OnboardingStepControl(OnboardingViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
    }

    public virtual Task<bool> ValidateAsync() => Task.FromResult(true);
    public virtual Task ApplyAsync() => Task.CompletedTask;
}
