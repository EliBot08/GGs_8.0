using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using GGs.Desktop.Services;
using GGs.Desktop.Services.Logging;

namespace GGs.Desktop.Views
{
    public partial class WelcomeOverlay : UserControl
    {
        private bool _isFirstRun;
        private TaskCompletionSource<bool>? _completionSource;

        public WelcomeOverlay()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Start fade-in animation
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(OpacityProperty, fadeIn);
        }

        public void SetFirstRun(bool isFirstRun)
        {
            _isFirstRun = isFirstRun;
        }

        public void UpdateStatus(string status)
        {
            Dispatcher.Invoke(() =>
            {
                InitStatusText.Text = status;
            });
        }

        public void UpdateTitle(string title, string? subtitle = null)
        {
            Dispatcher.Invoke(() =>
            {
                WelcomeTitle.Text = title;
                if (subtitle != null)
                {
                    WelcomeSubtitle.Text = subtitle;
                }
            });
        }

        public async Task ShowCompletionAsync()
        {
            _completionSource = new TaskCompletionSource<bool>();

            await Dispatcher.InvokeAsync(() =>
            {
                // Hide progress bar
                InitProgressBar.Visibility = Visibility.Collapsed;
                InitStatusText.Visibility = Visibility.Collapsed;

                if (_isFirstRun)
                {
                    // Show first-run checklist
                    WelcomeTitle.Text = "Welcome to GGs Pro";
                    WelcomeSubtitle.Text = "Everything is ready to go!";
                    FirstRunPanel.Visibility = Visibility.Visible;

                    // Animate checklist in
                    var slideUp = new DoubleAnimation
                    {
                        From = 30,
                        To = 0,
                        Duration = TimeSpan.FromMilliseconds(400),
                        EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 4 }
                    };
                    var fadeIn = new DoubleAnimation
                    {
                        From = 0,
                        To = 1,
                        Duration = TimeSpan.FromMilliseconds(400)
                    };

                    var transform = new System.Windows.Media.TranslateTransform();
                    FirstRunPanel.RenderTransform = transform;
                    transform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slideUp);
                    FirstRunPanel.BeginAnimation(OpacityProperty, fadeIn);
                }
                else
                {
                    // Auto-dismiss after a short delay for returning users
                    WelcomeTitle.Text = "Welcome Back";
                    WelcomeSubtitle.Text = "Ready to optimize!";
                    
                    Task.Delay(1000).ContinueWith(_ =>
                    {
                        _completionSource?.TrySetResult(true);
                    });
                }
            });

            await _completionSource.Task;
        }

        private void GetStartedButton_Click(object sender, RoutedEventArgs e)
        {
            AppLogger.LogInfo("User clicked Get Started button");
            _completionSource?.TrySetResult(true);
        }

        public async Task HideAsync()
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                // Fade out animation
                var fadeOut = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(300),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };

                var tcs = new TaskCompletionSource<bool>();
                fadeOut.Completed += (s, e) => tcs.SetResult(true);
                BeginAnimation(OpacityProperty, fadeOut);

                await tcs.Task;
                Visibility = Visibility.Collapsed;
            });
        }
    }
}

