using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using GGs.Desktop.Services;

namespace GGs.Desktop.Views;

public partial class ModernActivationWindow : Window
{
    private readonly LicenseService _licenseService;
    private bool _isProcessing = false;

    public ModernActivationWindow()
    {
        InitializeComponent();
        try { IconService.ApplyWindowIcon(this); } catch { }
        _licenseService = new LicenseService();
        
        // Initialize StatusMessage transform
        StatusMessage.RenderTransform = new System.Windows.Media.TranslateTransform();
        
        // Start fade-in animation
        this.Opacity = 0;
        this.Loaded += (s, e) =>
        {
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(500));
            this.BeginAnimation(OpacityProperty, fadeIn);
            
            // Focus on license key box
            LicenseKeyBox.Focus();
            
            // Update placeholder visibility
            UpdatePlaceholderVisibility();
        };
    }

    private void UpdatePlaceholderVisibility()
    {
        if (PlaceholderText != null)
        {
            PlaceholderText.Visibility = string.IsNullOrEmpty(LicenseKeyBox.Text) ? 
                Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void PasteButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Clipboard.ContainsText())
            {
                string clipboardText = Clipboard.GetText();
                // Remove any existing formatting and only keep alphanumeric
                string cleanedText = Regex.Replace(clipboardText, @"[^A-Za-z0-9]", "");
                
                if (!string.IsNullOrEmpty(cleanedText))
                {
                    // Take only first 16 characters if longer
                    if (cleanedText.Length > 16)
                        cleanedText = cleanedText.Substring(0, 16);
                    
                    // Format and set the text
                    LicenseKeyBox.Text = cleanedText;
                    LicenseKeyBox.Focus();
                    LicenseKeyBox.CaretIndex = LicenseKeyBox.Text.Length;
                    
                    // Show feedback
                    ShowStatusMessage("License key pasted from clipboard", false);
                }
                else
                {
                    ShowStatusMessage("No valid license key found in clipboard", true);
                }
            }
            else
            {
                ShowStatusMessage("Clipboard is empty", true);
            }
        }
        catch (Exception ex)
        {
            ShowStatusMessage($"Failed to paste: {ex.Message}", true);
        }
    }

    private void BuyNowButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Open purchase page in default browser
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://discord.gg/3pBX9ymWRU",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ShowStatusMessage($"Failed to open purchase page: {ex.Message}", true);
        }
    }

    private void ShowStatusMessage(string message, bool isError)
    {
        StatusMessage.Text = message;
        StatusMessage.Foreground = new System.Windows.Media.SolidColorBrush(
            isError ? System.Windows.Media.Color.FromRgb(239, 68, 68) : 
                      System.Windows.Media.Color.FromRgb(156, 163, 175));
        StatusMessage.Visibility = Visibility.Visible;
        
        // Auto-hide after 3 seconds
        var timer = new System.Windows.Threading.DispatcherTimer();
        timer.Interval = TimeSpan.FromSeconds(3);
        timer.Tick += (s, e) =>
        {
            StatusMessage.Visibility = Visibility.Collapsed;
            timer.Stop();
        };
        timer.Start();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
            fadeOut.Completed += (s, args) =>
            {
                try
                {
                    var licensed = new LicenseService().CurrentPayload != null;
                    if (!licensed)
                    {
                        // Unlicensed: fully exit the app per requirement
                        Application.Current?.Shutdown();
                        return;
                    }
                    // Licensed: just close this window (user may have opened by mistake)
                    this.Close();
                }
                catch { Application.Current?.Shutdown(); }
            };
            this.BeginAnimation(OpacityProperty, fadeOut);
        }
        catch
        {
            // Fallback
            var licensed = new LicenseService().CurrentPayload != null;
            if (!licensed) { Application.Current?.Shutdown(); return; }
            try { this.Close(); } catch { }
        }
    }

    private void LicenseKeyBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_isProcessing) return;
        _isProcessing = true;

        var text = LicenseKeyBox.Text.Replace("-", "").ToUpper();
        var formatted = "";

        // Format as XXXX-XXXX-XXXX-XXXX
        for (int i = 0; i < text.Length && i < 16; i++)
        {
            if (i > 0 && i % 4 == 0)
                formatted += "-";
            formatted += text[i];
        }

        LicenseKeyBox.Text = formatted;
        LicenseKeyBox.CaretIndex = formatted.Length;

        // Enable activate button when we have 16 characters
        var cleanKey = formatted.Replace("-", "");
        ActivateButton.IsEnabled = cleanKey.Length == 16 && IsValidKeyFormat(cleanKey);
        
        // Update placeholder visibility
        UpdatePlaceholderVisibility();

        _isProcessing = false;
    }

    private void LicenseKeyBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // Only allow alphanumeric characters
        e.Handled = !Regex.IsMatch(e.Text, @"^[a-zA-Z0-9]+$");
    }

private async void LicenseKeyBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ActivateButton.IsEnabled)
        {
            // Trigger license activation same as button click
            await ActivateLicense();
            e.Handled = true;
        }
    }

    private bool IsValidKeyFormat(string key)
    {
        // Check if key contains only alphanumeric characters
        return Regex.IsMatch(key, @"^[A-Z0-9]{16}$");
    }

    private async void ActivateButton_Click(object sender, RoutedEventArgs e)
    {
        await ActivateLicense();
    }

    private async System.Threading.Tasks.Task ActivateLicense()
    {
        try
        {
            var keyText = LicenseKeyBox.Text ?? string.Empty;

            // Show processing state
            ActivateButton.IsEnabled = false;
            ActivateButton.Content = "Logging in...";
            StatusMessage.Visibility = Visibility.Visible;
            StatusMessage.Text = "Validating license...";
            StatusMessage.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(156, 163, 175));

            // Animate status message
            var slideUp = new DoubleAnimation(30, 0, TimeSpan.FromMilliseconds(400));
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400));
            StatusMessage.RenderTransform.BeginAnimation(
                System.Windows.Media.TranslateTransform.YProperty, slideUp);
            StatusMessage.BeginAnimation(OpacityProperty, fadeIn);

            // Unified license handling: JSON license or 16-char demo key
            var (ok, msg) = await _licenseService.ValidateAndSaveFromTextAsync(keyText);

            if (ok)
            {
                StatusMessage.Text = msg ?? "✓ License activated successfully!";
                StatusMessage.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0, 255, 136));
                
                await System.Threading.Tasks.Task.Delay(500);
                
                // Transition to modern main window with error handling
                try
                {
                    // Ensure tray is initialized now that we are licensed
                    try { GGs.Desktop.Services.TrayIconService.Instance.Initialize(); } catch { }
                    var mainWindow = new ModernMainWindow();
                    mainWindow.Show();
                    
                    var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
                    fadeOut.Completed += (s, args) => 
                    {
                        try { this.Close(); } catch {}
                    };
                    this.BeginAnimation(OpacityProperty, fadeOut);
                }
                catch (Exception ex)
                {
                    // If main window fails to open, show error and re-enable activation
                    GGs.Desktop.Services.AppLogger.LogError("Failed to open ModernMainWindow from activation window", ex);
                    StatusMessage.Text = $"✗ Error opening main application: {ex.Message}";
                    StatusMessage.Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(255, 20, 100));
                    
                    ActivateButton.IsEnabled = true;
                    ActivateButton.Content = "Log in";
                }
            }
            else
            {
                StatusMessage.Text = $"✗ {msg ?? "Invalid license. Please check and try again."}";
                StatusMessage.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(255, 20, 100));
                
                ActivateButton.IsEnabled = true;
                ActivateButton.Content = "Log in";
                
                // Shake animation for error
                var shake = new DoubleAnimationUsingKeyFrames();
                shake.KeyFrames.Add(new LinearDoubleKeyFrame(0, TimeSpan.FromMilliseconds(0)));
                shake.KeyFrames.Add(new LinearDoubleKeyFrame(-10, TimeSpan.FromMilliseconds(50)));
                shake.KeyFrames.Add(new LinearDoubleKeyFrame(10, TimeSpan.FromMilliseconds(150)));
                shake.KeyFrames.Add(new LinearDoubleKeyFrame(-10, TimeSpan.FromMilliseconds(250)));
                shake.KeyFrames.Add(new LinearDoubleKeyFrame(0, TimeSpan.FromMilliseconds(350)));
                LicenseKeyBox.RenderTransform = new System.Windows.Media.TranslateTransform();
                LicenseKeyBox.RenderTransform.BeginAnimation(
                    System.Windows.Media.TranslateTransform.XProperty, shake);
            }
        }
        catch (Exception ex)
        {
            // Handle any unexpected errors gracefully
            StatusMessage.Text = $"✗ Unexpected error: {ex.Message}";
            StatusMessage.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(255, 20, 100));
            StatusMessage.Visibility = Visibility.Visible;
            
            ActivateButton.IsEnabled = true;
            ActivateButton.Content = "Log in";
            
            // Log the error for debugging
            System.Diagnostics.Debug.WriteLine($"License activation error: {ex}");
        }
    }

    private bool IsValidDemoKey(string key)
    {
        // Fixed license keys - properly formatted
        var validKeys = new[]
        {
            "GGSPRO2024ENTERP", // Enterprise key  
            "GGSP2024ADMINKEY", // Admin key
            "GGSPRO2024PROFES", // Pro key
            "1234567890ABCDEF", // Test key
            "DEMOKEY123456789"  // Demo key
        };

        return Array.Exists(validKeys, k => k == key.ToUpper().Replace("-", ""));
    }
    
    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }
    
    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }
}
