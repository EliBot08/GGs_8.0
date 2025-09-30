using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.Logging;

namespace GGs.Desktop.Services;

public interface IAccessibilityService
{
    bool IsScreenReaderActive { get; }
    bool IsHighContrastEnabled { get; }
    bool IsKeyboardNavigationEnabled { get; }
    void AnnounceToScreenReader(string message);
    void SetAccessibilityProperties(FrameworkElement element, string name, string description, string role = "");
    void EnableKeyboardNavigation(Panel container);
    void ConfigureForAccessibility(Window window);
    event EventHandler<AccessibilityChangedEventArgs> AccessibilitySettingsChanged;
}

public sealed class AccessibilityService : IAccessibilityService
{
    private readonly ILogger<AccessibilityService> _logger;
    private bool _isScreenReaderActive;
    private bool _isHighContrastEnabled;
    private bool _isKeyboardNavigationEnabled;

    public bool IsScreenReaderActive => _isScreenReaderActive;
    public bool IsHighContrastEnabled => _isHighContrastEnabled;
    public bool IsKeyboardNavigationEnabled => _isKeyboardNavigationEnabled;

    public event EventHandler<AccessibilityChangedEventArgs>? AccessibilitySettingsChanged;

    public AccessibilityService(ILogger<AccessibilityService> logger)
    {
        _logger = logger;
        InitializeAccessibilityState();
        
        // Monitor system accessibility changes
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    private void InitializeAccessibilityState()
    {
        try
        {
            _isScreenReaderActive = DetectScreenReader();
            _isHighContrastEnabled = SystemParameters.HighContrast;
            _isKeyboardNavigationEnabled = true; // Always enable for better UX
            
            _logger.LogInformation("Accessibility state initialized: ScreenReader={ScreenReader}, HighContrast={HighContrast}, KeyboardNav={KeyboardNav}",
                _isScreenReaderActive, _isHighContrastEnabled, _isKeyboardNavigationEnabled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize accessibility state");
        }
    }

    private bool DetectScreenReader()
    {
        try
        {
            // Check for JAWS
            if (FindWindow("JFWUI2", null!) != IntPtr.Zero)
                return true;

            // Check for NVDA
            if (FindWindow("wxWindowClassNR", "NVDA") != IntPtr.Zero)
                return true;

            // Check for Windows Narrator
            if (FindWindow("Windows.UI.Core.CoreWindow", "Narrator") != IntPtr.Zero)
                return true;

            // Check system setting
            return SystemInformation.IsScreenReaderPresent;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to detect screen reader");
            return false;
        }
    }

    public void AnnounceToScreenReader(string message)
    {
        if (!_isScreenReaderActive || string.IsNullOrEmpty(message))
            return;

        try
        {
            // Use UIA to announce to screen readers
            if (System.Windows.Automation.Peers.AutomationPeer.ListenerExists(System.Windows.Automation.Peers.AutomationEvents.PropertyChanged))
            {
                // Note: AutomationInteropProvider is not available in .NET 8, using alternative approach
                // This is a simplified implementation for .NET 8 compatibility
                System.Diagnostics.Debug.WriteLine($"Screen reader announcement: {message}");
            }

            // Fallback: Create a temporary live region
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                var announcement = new TextBlock
                {
                    Text = message,
                    Visibility = Visibility.Collapsed
                };

                AutomationProperties.SetLiveSetting(announcement, AutomationLiveSetting.Assertive);
                AutomationProperties.SetName(announcement, message);

                if (Application.Current.MainWindow is Window mainWindow)
                {
                    if (mainWindow.Content is Panel panel)
                    {
                        panel.Children.Add(announcement);
                        
                        // Remove after announcement
                        var timer = new System.Windows.Threading.DispatcherTimer
                        {
                            Interval = TimeSpan.FromSeconds(2)
                        };
                        timer.Tick += (s, e) =>
                        {
                            timer.Stop();
                            panel.Children.Remove(announcement);
                        };
                        timer.Start();
                    }
                }
            });

            _logger.LogDebug("Announced to screen reader: {Message}", message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to announce to screen reader: {Message}", message);
        }
    }

    public void SetAccessibilityProperties(FrameworkElement element, string name, string description, string role = "")
    {
        try
        {
            if (!string.IsNullOrEmpty(name))
                AutomationProperties.SetName(element, name);

            if (!string.IsNullOrEmpty(description))
                AutomationProperties.SetHelpText(element, description);

            if (!string.IsNullOrEmpty(role))
                AutomationProperties.SetAutomationId(element, role);

            // Make element keyboard accessible
            if (element.Focusable)
            {
                element.KeyDown += OnAccessibleElementKeyDown;
            }

            _logger.LogDebug("Set accessibility properties for element: Name={Name}, Description={Description}, Role={Role}",
                name, description, role);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set accessibility properties");
        }
    }

    public void EnableKeyboardNavigation(Panel container)
    {
        try
        {
            // Enable tab navigation
            KeyboardNavigation.SetTabNavigation(container, KeyboardNavigationMode.Cycle);
            KeyboardNavigation.SetDirectionalNavigation(container, KeyboardNavigationMode.Cycle);
            
            // Add keyboard handlers
            container.KeyDown += OnContainerKeyDown;
            container.PreviewKeyDown += OnContainerPreviewKeyDown;

            // Make all interactive elements keyboard accessible
            foreach (UIElement child in container.Children)
            {
                if (child is Control control)
                {
                    EnsureKeyboardAccessible(control);
                }
            }

            _logger.LogDebug("Enabled keyboard navigation for container");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable keyboard navigation");
        }
    }

    public void ConfigureForAccessibility(Window window)
    {
        try
        {
            // Set window properties
            AutomationProperties.SetName(window, window.Title);
            AutomationProperties.SetAutomationId(window, "MainWindow");

            // Enable keyboard navigation
            window.KeyDown += OnWindowKeyDown;
            
            // Add global keyboard shortcuts
            AddGlobalKeyboardShortcuts(window);

            // Configure for high contrast if enabled
            if (_isHighContrastEnabled)
            {
                ConfigureHighContrast(window);
            }

            // Announce window opening to screen reader
            if (_isScreenReaderActive)
            {
                AnnounceToScreenReader($"{window.Title} window opened");
            }

            _logger.LogInformation("Configured window for accessibility: {WindowTitle}", window.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure window for accessibility");
        }
    }

    private void EnsureKeyboardAccessible(Control control)
    {
        // Make focusable if it's interactive
        if (control is Button || control is TextBox || control is ComboBox || control is CheckBox)
        {
            control.IsTabStop = true;
            control.Focusable = true;
        }

        // Add keyboard handlers for custom behavior
        control.KeyDown += OnControlKeyDown;
        control.GotFocus += OnControlGotFocus;
        control.LostFocus += OnControlLostFocus;
    }

    private void AddGlobalKeyboardShortcuts(Window window)
    {
        // Add common accessibility shortcuts
        var shortcuts = new Dictionary<Key, Action>
        {
            { Key.F1, () => ShowHelp() },
            { Key.F6, () => CycleFocus(window) },
            { Key.Escape, () => HandleEscape(window) }
        };

        window.InputBindings.Clear();
        foreach (var shortcut in shortcuts)
        {
            window.InputBindings.Add(new KeyBinding(new RelayCommand(shortcut.Value), shortcut.Key, ModifierKeys.None));
        }

        // Ctrl shortcuts
        var ctrlShortcuts = new Dictionary<Key, Action>
        {
            { Key.OemPlus, () => IncreaseTextSize() },
            { Key.OemMinus, () => DecreaseTextSize() },
            { Key.D0, () => ResetTextSize() }
        };

        foreach (var shortcut in ctrlShortcuts)
        {
            window.InputBindings.Add(new KeyBinding(new RelayCommand(shortcut.Value), shortcut.Key, ModifierKeys.Control));
        }
    }

    private void ConfigureHighContrast(Window window)
    {
        // Apply high contrast theme
        var highContrastResources = new ResourceDictionary
        {
            Source = new Uri("/Themes/HighContrastTheme.xaml", UriKind.Relative)
        };
        
        window.Resources.MergedDictionaries.Add(highContrastResources);
        _logger.LogInformation("Applied high contrast theme");
    }

    private void OnUserPreferenceChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        if (e.Category == Microsoft.Win32.UserPreferenceCategory.Accessibility)
        {
            var oldScreenReader = _isScreenReaderActive;
            var oldHighContrast = _isHighContrastEnabled;

            InitializeAccessibilityState();

            if (oldScreenReader != _isScreenReaderActive || oldHighContrast != _isHighContrastEnabled)
            {
                AccessibilitySettingsChanged?.Invoke(this, new AccessibilityChangedEventArgs
                {
                    ScreenReaderChanged = oldScreenReader != _isScreenReaderActive,
                    HighContrastChanged = oldHighContrast != _isHighContrastEnabled
                });
            }
        }
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        // Handle global window shortcuts
        if (e.Key == Key.F1)
        {
            ShowHelp();
            e.Handled = true;
        }
    }

    private void OnContainerKeyDown(object sender, KeyEventArgs e)
    {
        // Handle container-specific navigation
        if (e.Key == Key.Tab)
        {
            // Custom tab handling if needed
        }
    }

    private void OnContainerPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Handle arrow key navigation
        if (e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Down)
        {
            // Implement arrow key navigation logic
        }
    }

    private void OnAccessibleElementKeyDown(object sender, KeyEventArgs e)
    {
        // Handle element-specific accessibility keys
        if (e.Key == Key.Enter || e.Key == Key.Space)
        {
            if (sender is Button button)
            {
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                e.Handled = true;
            }
        }
    }

    private void OnControlKeyDown(object sender, KeyEventArgs e)
    {
        // Announce control information to screen reader
        if (_isScreenReaderActive && sender is Control control)
        {
            var name = AutomationProperties.GetName(control);
            var helpText = AutomationProperties.GetHelpText(control);
            
            if (!string.IsNullOrEmpty(name))
            {
                AnnounceToScreenReader($"Focused on {name}");
            }
        }
    }

    private void OnControlGotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is Control control && _isScreenReaderActive)
        {
            var name = AutomationProperties.GetName(control);
            if (!string.IsNullOrEmpty(name))
            {
                AnnounceToScreenReader($"Focused on {name}");
            }
        }
    }

    private void OnControlLostFocus(object sender, RoutedEventArgs e)
    {
        // Handle focus lost if needed
    }

    private void ShowHelp()
    {
        AnnounceToScreenReader("Help: Use Tab to navigate between controls, Enter or Space to activate buttons, Arrow keys for navigation.");
    }

    private void CycleFocus(Window window)
    {
        window.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
    }

    private void HandleEscape(Window window)
    {
        // Find and focus the main content or close dialogs
        if (window.DialogResult.HasValue)
        {
            window.DialogResult = false;
        }
    }

    private void IncreaseTextSize()
    {
        AdjustTextSize(1.1);
    }

    private void DecreaseTextSize()
    {
        AdjustTextSize(0.9);
    }

    private void ResetTextSize()
    {
        SetTextSizeScale(1.0);
    }

    private void AdjustTextSize(double factor)
    {
        if (Application.Current.MainWindow != null)
        {
            var currentScale = Application.Current.MainWindow.LayoutTransform as ScaleTransform 
                ?? new ScaleTransform(1, 1);
            
            var newScale = Math.Min(Math.Max(currentScale.ScaleX * factor, 0.8), 2.0);
            SetTextSizeScale(newScale);
        }
    }

    private void SetTextSizeScale(double scale)
    {
        if (Application.Current.MainWindow != null)
        {
            Application.Current.MainWindow.LayoutTransform = new ScaleTransform(scale, scale);
            AnnounceToScreenReader($"Text size set to {scale:P0}");
        }
    }

    // P/Invoke declarations
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    // Dispose pattern
    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }
}

public class AccessibilityChangedEventArgs : EventArgs
{
    public bool ScreenReaderChanged { get; set; }
    public bool HighContrastChanged { get; set; }
}

// Helper command class
public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();
}

// System events helper
internal static class SystemEvents
{
    public static event Microsoft.Win32.UserPreferenceChangedEventHandler? UserPreferenceChanged
    {
        add { Microsoft.Win32.SystemEvents.UserPreferenceChanged += value; }
        remove { Microsoft.Win32.SystemEvents.UserPreferenceChanged -= value; }
    }
}

// System information helper
internal static class SystemInformation
{
    [DllImport("user32.dll")]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref bool pvParam, uint fWinIni);

    private const uint SPI_GETSCREENREADER = 0x0046;

    public static bool IsScreenReaderPresent
    {
        get
        {
            bool screenReader = false;
            SystemParametersInfo(SPI_GETSCREENREADER, 0, ref screenReader, 0);
            return screenReader;
        }
    }
}
