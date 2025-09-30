using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace GGs.Desktop.Views.Controls;

/// <summary>
/// Professional animated progress bar with smooth transitions and visual effects
/// </summary>
public partial class AnimatedProgressBar : UserControl
{
    private DispatcherTimer? _iconRotationTimer;
    private Storyboard? _currentAnimation;
    private double _currentProgress = 0;
    private bool _isCompleted = false;

    public static readonly DependencyProperty ProgressProperty =
        DependencyProperty.Register(nameof(Progress), typeof(double), typeof(AnimatedProgressBar),
            new PropertyMetadata(0.0, OnProgressChanged));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(AnimatedProgressBar),
            new PropertyMetadata("Processing...", OnDescriptionChanged));

    public static readonly DependencyProperty StepProperty =
        DependencyProperty.Register(nameof(Step), typeof(int), typeof(AnimatedProgressBar),
            new PropertyMetadata(1, OnStepChanged));

    public static readonly DependencyProperty TotalStepsProperty =
        DependencyProperty.Register(nameof(TotalSteps), typeof(int), typeof(AnimatedProgressBar),
            new PropertyMetadata(10, OnTotalStepsChanged));

    public static readonly DependencyProperty AnimationTypeProperty =
        DependencyProperty.Register(nameof(AnimationType), typeof(ProgressAnimationType), typeof(AnimatedProgressBar),
            new PropertyMetadata(ProgressAnimationType.Processing, OnAnimationTypeChanged));

    public static readonly DependencyProperty IsCompletedProperty =
        DependencyProperty.Register(nameof(IsCompleted), typeof(bool), typeof(AnimatedProgressBar),
            new PropertyMetadata(false, OnIsCompletedChanged));

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public int Step
    {
        get => (int)GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    public int TotalSteps
    {
        get => (int)GetValue(TotalStepsProperty);
        set => SetValue(TotalStepsProperty, value);
    }

    public ProgressAnimationType AnimationType
    {
        get => (ProgressAnimationType)GetValue(AnimationTypeProperty);
        set => SetValue(AnimationTypeProperty, value);
    }

    public bool IsCompleted
    {
        get => (bool)GetValue(IsCompletedProperty);
        set => SetValue(IsCompletedProperty, value);
    }

    public AnimatedProgressBar()
    {
        InitializeComponent();
        InitializeAnimations();
        Unloaded += OnUnloaded;
    }

    private void InitializeAnimations()
    {
        // Start icon rotation animation
        _iconRotationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _iconRotationTimer.Tick += (s, e) =>
        {
            if (!_isCompleted)
            {
                IconRotateTransform.Angle = (IconRotateTransform.Angle + 2) % 360;
            }
        };
        _iconRotationTimer.Start();

        // Start pulse animation
        var pulseStoryboard = (Storyboard)Resources["PulseAnimation"];
        pulseStoryboard.Begin();

        // Start shimmer animation
        var shimmerStoryboard = (Storyboard)Resources["ShimmerAnimation"];
        shimmerStoryboard.Begin();
    }

    private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AnimatedProgressBar progressBar)
        {
            progressBar.UpdateProgress((double)e.NewValue);
        }
    }

    private static void OnDescriptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AnimatedProgressBar progressBar)
        {
            progressBar.DescriptionText.Text = (string)e.NewValue;
        }
    }

    private static void OnStepChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AnimatedProgressBar progressBar)
        {
            progressBar.UpdateStepDisplay();
        }
    }

    private static void OnTotalStepsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AnimatedProgressBar progressBar)
        {
            progressBar.UpdateStepDisplay();
        }
    }

    private static void OnAnimationTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AnimatedProgressBar progressBar)
        {
            progressBar.UpdateAnimationType((ProgressAnimationType)e.NewValue);
        }
    }

    private static void OnIsCompletedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AnimatedProgressBar progressBar)
        {
            progressBar.UpdateCompletionState((bool)e.NewValue);
        }
    }

    private void UpdateProgress(double newProgress)
    {
        var clampedProgress = Math.Max(0, Math.Min(100, newProgress));
        
        // Animate progress bar fill
        var scaleAnimation = new DoubleAnimation
        {
            From = _currentProgress / 100.0,
            To = clampedProgress / 100.0,
            Duration = TimeSpan.FromMilliseconds(500),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        ProgressScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);

        // Animate percentage text
        var percentageAnimation = new DoubleAnimation
        {
            From = _currentProgress,
            To = clampedProgress,
            Duration = TimeSpan.FromMilliseconds(500),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        percentageAnimation.CurrentTimeInvalidated += (s, e) =>
        {
            // Update progress text during animation
            var currentValue = _currentProgress + (clampedProgress - _currentProgress) * 0.5; // Approximate progress
            PercentageText.Text = $"{currentValue:F0}%";
        };

        this.BeginAnimation(ProgressProperty, percentageAnimation);
        _currentProgress = clampedProgress;

        // Update progress bar color based on progress
        UpdateProgressColor(clampedProgress);
    }

    private void UpdateProgressColor(double progress)
    {
        Brush progressBrush;

        if (progress >= 100)
        {
            progressBrush = (Brush)Resources["ProgressGradientSuccess"];
        }
        else if (progress >= 75)
        {
            progressBrush = (Brush)Resources["ProgressGradient"];
        }
        else if (progress >= 50)
        {
            progressBrush = (Brush)Resources["ProgressGradient"];
        }
        else if (progress >= 25)
        {
            progressBrush = (Brush)Resources["ProgressGradientWarning"];
        }
        else
        {
            progressBrush = (Brush)Resources["ProgressGradient"];
        }

        var colorAnimation = new ColorAnimation
        {
            Duration = TimeSpan.FromMilliseconds(300),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        ProgressFill.Fill = progressBrush;
    }

    private void UpdateStepDisplay()
    {
        StepText.Text = $"Step {Step} of {TotalSteps}";
    }

    private void UpdateAnimationType(ProgressAnimationType animationType)
    {
        var iconText = animationType switch
        {
            ProgressAnimationType.Scanning => "🔍",
            ProgressAnimationType.Processing => "⚙️",
            ProgressAnimationType.Optimizing => "⚡",
            ProgressAnimationType.Securing => "🛡️",
            ProgressAnimationType.Networking => "🌐",
            ProgressAnimationType.Graphics => "🎮",
            ProgressAnimationType.Memory => "💾",
            ProgressAnimationType.Storage => "💿",
            ProgressAnimationType.Power => "🔋",
            ProgressAnimationType.Gaming => "🎯",
            ProgressAnimationType.Privacy => "🔒",
            ProgressAnimationType.Services => "⚙️",
            ProgressAnimationType.Advanced => "🚀",
            ProgressAnimationType.Completing => "✨",
            _ => "🔍"
        };

        AnimationIcon.Text = iconText;

        // Add special effects for certain animation types
        if (animationType == ProgressAnimationType.Completing)
        {
            var sparkleAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 1.5,
                Duration = TimeSpan.FromMilliseconds(300),
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(3)
            };

            var scaleTransform = new ScaleTransform();
            AnimationIcon.RenderTransform = scaleTransform;
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, sparkleAnimation);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, sparkleAnimation);
        }
    }

    private void UpdateCompletionState(bool isCompleted)
    {
        _isCompleted = isCompleted;

        if (isCompleted)
        {
            // Stop icon rotation
            _iconRotationTimer?.Stop();
            IconRotateTransform.Angle = 0;

            // Show completion icon
            CompletionIcon.Visibility = Visibility.Visible;
            var completionStoryboard = (Storyboard)Resources["CompletionAnimation"];
            completionStoryboard.Begin();

            // Update progress to 100%
            Progress = 100;

            // Change description to completion message
            DescriptionText.Text = "✅ Completed successfully!";
            DescriptionText.Foreground = (Brush)FindResource("ThemeSuccess");
        }
        else
        {
            // Hide completion icon
            CompletionIcon.Visibility = Visibility.Collapsed;
            CompletionIcon.Opacity = 0;

            // Restart icon rotation if needed
            if (!_iconRotationTimer.IsEnabled)
            {
                _iconRotationTimer.Start();
            }
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _iconRotationTimer?.Stop();
        _currentAnimation?.Stop();
    }
}

/// <summary>
/// Animation types for different progress operations
/// </summary>
public enum ProgressAnimationType
{
    Scanning,
    Processing,
    Optimizing,
    Securing,
    Networking,
    Graphics,
    Memory,
    Storage,
    Power,
    Gaming,
    Privacy,
    Services,
    Advanced,
    Completing
}