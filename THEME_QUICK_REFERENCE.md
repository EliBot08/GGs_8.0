# 🎨 GGs Theme & Animation Quick Reference

## 🚀 Quick Start

### Using Enterprise Themes in Your XAML

```xaml
<!-- Import the enterprise themes -->
<Window.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="/Themes/EnterpriseThemes.xaml"/>
            <ResourceDictionary Source="/Themes/EnterpriseControlStyles.xaml"/>
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Window.Resources>
```

---

## 🎨 Color Reference

### Dark Theme (Midnight)
```xaml
<!-- Backgrounds -->
{DynamicResource ThemeBackgroundPrimary}      <!-- #0A0E27 Deep Navy -->
{DynamicResource ThemeBackgroundSecondary}    <!-- #141B3D Navy Blue -->
{DynamicResource ThemeBackgroundTertiary}     <!-- #1C2647 Dark Blue -->
{DynamicResource ThemeSurface}                <!-- #141B3D -->

<!-- Accents -->
{DynamicResource ThemeAccentPrimary}          <!-- #00E5FF Cyan -->
{DynamicResource ThemeAccentSecondary}        <!-- #00B8D4 Dark Cyan -->
{DynamicResource ThemeAccentGradient}         <!-- Gradient -->

<!-- Text -->
{DynamicResource ThemeTextPrimary}            <!-- #FFFFFF White -->
{DynamicResource ThemeTextSecondary}          <!-- #B8C5E0 Light Gray -->

<!-- Status -->
{DynamicResource ThemeSuccess}                <!-- #00E676 Green -->
{DynamicResource ThemeWarning}                <!-- #FFAB00 Amber -->
{DynamicResource ThemeError}                  <!-- #FF1744 Red -->
```

### Light Theme
```xaml
<!-- Backgrounds -->
{DynamicResource ThemeBackgroundPrimary}      <!-- #FFFFFF White -->
{DynamicResource ThemeBackgroundSecondary}    <!-- #F7F9FC Off-White -->

<!-- Accents -->
{DynamicResource ThemeAccentPrimary}          <!-- #2563EB Blue -->
{DynamicResource ThemeAccentGradient}         <!-- Blue Gradient -->

<!-- Text -->
{DynamicResource ThemeTextPrimary}            <!-- #1A202C Dark -->
{DynamicResource ThemeTextSecondary}          <!-- #4A5568 Gray -->
```

---

## 🎯 Control Styles

### Buttons
```xaml
<!-- Primary Button (with glow) -->
<Button Style="{StaticResource PrimaryButton}" Content="Save"/>

<!-- Secondary Button (outlined) -->
<Button Style="{StaticResource SecondaryButton}" Content="Cancel"/>

<!-- Icon Button (small, subtle) -->
<Button Style="{StaticResource IconButton}" Content="🔧"/>
```

### Inputs
```xaml
<!-- Enterprise TextBox (focus glow) -->
<TextBox Style="{StaticResource EnterpriseTextBox}" 
         PlaceholderText="Enter text..."/>
```

### Containers
```xaml
<!-- Card with hover effect -->
<Border Style="{StaticResource EnterpriseCard}">
    <TextBlock Text="Content"/>
</Border>
```

### Progress
```xaml
<!-- Animated Progress Bar -->
<ProgressBar Style="{StaticResource EnterpriseProgressBar}" 
             Value="50" Maximum="100"/>
```

---

## ✨ Animations

### Apply on Load
```xaml
<Border>
    <Border.Triggers>
        <EventTrigger RoutedEvent="Loaded">
            <BeginStoryboard Storyboard="{StaticResource FadeIn}"/>
        </EventTrigger>
    </Border.Triggers>
</Border>
```

### Available Animations
```xaml
{StaticResource FadeIn}         <!-- Fade in (350ms) -->
{StaticResource SlideUp}        <!-- Slide up + fade (450ms) -->
{StaticResource ScaleIn}        <!-- Scale + bounce (400ms) -->
{StaticResource SlideFromLeft}  <!-- Slide from left (500ms) -->
{StaticResource Pulse}          <!-- Infinite pulse -->
{StaticResource Glow}           <!-- Infinite glow -->
```

### Manual Animation
```xaml
<Button>
    <Button.Triggers>
        <EventTrigger RoutedEvent="Click">
            <BeginStoryboard>
                <Storyboard>
                    <DoubleAnimation 
                        Storyboard.TargetProperty="Opacity"
                        From="0" To="1" Duration="0:0:0.3"/>
                </Storyboard>
            </BeginStoryboard>
        </EventTrigger>
    </Button.Triggers>
</Button>
```

---

## 🎭 Theme Switching (C#)

### Get Theme Manager
```csharp
using GGs.Desktop.Services;

var themeManager = ThemeManagerService.Instance;
```

### Switch Themes
```csharp
// Switch to dark
themeManager.CurrentTheme = AppTheme.Dark;

// Switch to light
themeManager.CurrentTheme = AppTheme.Light;

// Use system theme
themeManager.CurrentTheme = AppTheme.System;

// Check current mode
bool isDark = themeManager.IsDarkMode;
```

### Listen for Theme Changes
```csharp
themeManager.ThemeChanged += (sender, newTheme) =>
{
    // Handle theme change
    Console.WriteLine($"Theme changed to: {newTheme}");
};
```

---

## 🎨 Custom Colors

### Override Accent Color
```csharp
using System.Windows.Media;

var themeManager = ThemeManagerService.Instance;
themeManager.SetAccentColor(Color.FromRgb(255, 0, 128)); // Custom pink
```

### Get Theme Colors in Code
```csharp
// Dark theme colors
var bgColor = ThemeManagerService.DarkTheme.BackgroundPrimary;
var accentColor = ThemeManagerService.DarkTheme.AccentPrimary;

// Light theme colors  
var lightBg = ThemeManagerService.LightTheme.BackgroundPrimary;
```

---

## 📦 Complete Example

```xaml
<Window x:Class="MyWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Background="{DynamicResource ThemeBackgroundPrimary}">
    
    <Window.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="/Themes/EnterpriseThemes.xaml"/>
                <ResourceDictionary Source="/Themes/EnterpriseControlStyles.xaml"/>
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Window.Resources>
    
    <Window.Triggers>
        <EventTrigger RoutedEvent="Loaded">
            <BeginStoryboard Storyboard="{StaticResource FadeIn}"/>
        </EventTrigger>
    </Window.Triggers>
    
    <Grid Margin="40">
        <!-- Card with animation -->
        <Border Style="{StaticResource EnterpriseCard}">
            <Border.Triggers>
                <EventTrigger RoutedEvent="Loaded">
                    <BeginStoryboard Storyboard="{StaticResource SlideUp}"/>
                </EventTrigger>
            </Border.Triggers>
            
            <StackPanel Spacing="20">
                <TextBlock Text="Welcome to GGs" 
                          FontSize="24" 
                          FontWeight="Bold"
                          Foreground="{DynamicResource ThemeTextPrimary}"/>
                
                <TextBox Style="{StaticResource EnterpriseTextBox}"
                        Text="Type here..."/>
                
                <Button Style="{StaticResource PrimaryButton}"
                       Content="Get Started"/>
            </StackPanel>
        </Border>
    </Grid>
</Window>
```

---

## 🔧 Troubleshooting

### Theme not applying?
1. Ensure themes are in `/Themes/` folder
2. Check `ResourceDictionary.MergedDictionaries`
3. Verify DynamicResource (not StaticResource)

### Animations not working?
1. Check `RenderTransform` is set on element
2. Ensure EventTrigger is correct
3. Verify Storyboard name is correct

### Colors look wrong?
1. Use `DynamicResource` for theme-switchable colors
2. Check if `ThemeManager.CurrentTheme` is set
3. Verify color keys exist in theme file

---

## 📚 Best Practices

✅ **DO:**
- Use `DynamicResource` for theme colors
- Apply animations on `Loaded` event
- Use enterprise styles for consistency
- Test in both light and dark modes

❌ **DON'T:**
- Hardcode colors (use theme resources)
- Use `StaticResource` for theme colors
- Skip accessibility testing
- Mix old and new themes

---

## 🎯 Pro Tips

1. **Performance:** Animations use GPU acceleration automatically
2. **Accessibility:** All themes meet WCAG AA contrast
3. **Customization:** Override theme colors in your App.xaml
4. **Testing:** Use `Ctrl+T` to toggle themes (if implemented)
5. **Debugging:** Use Snoop or Live Visual Tree to inspect themes

---

**Happy Theming! 🎨**
