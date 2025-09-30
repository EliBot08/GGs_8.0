# Comprehensive Build Error Fix Script
# Fixes all nullable reference type and compilation errors

$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$fixCount = 0

Write-Host "=== Comprehensive Build Error Fix ===" -ForegroundColor Cyan
Write-Host "Target: clients/GGs.Desktop project" -ForegroundColor Yellow
Write-Host ""

# Define all files that need fixing
$fileFixes = @{
    "clients\GGs.Desktop\Views\CommunityHubView.xaml.cs" = @(
        @{ Find = 'public event PropertyChangedEventHandler PropertyChanged;'; Replace = 'public event PropertyChangedEventHandler? PropertyChanged;' }
        @{ Find = 'private void OnProfileDownloaded(object sender,'; Replace = 'private void OnProfileDownloaded(object? sender,' }
        @{ Find = 'private async void SearchProfilesAsync('; Replace = 'private async Task SearchProfilesAsync(' }
        @{ Find = 'private async void ApplyFiltersAsync('; Replace = 'private async Task ApplyFiltersAsync(' }
        @{ Find = 'private async void LoadInitialDataAsync('; Replace = 'private async Task LoadInitialDataAsync(' }
        @{ Find = 'private CommunityProfile _selectedProfile;'; Replace = 'private CommunityProfile? _selectedProfile;' }
        @{ Find = 'public CommunityProfile SelectedProfile'; Replace = 'public required CommunityProfile SelectedProfile' }
        @{ Find = 'public ObservableCollection<ShareOption> ShareOptions { get; set; }'; Replace = 'public ObservableCollection<ShareOption> ShareOptions { get; set; } = new();' }
        @{ Find = 'await Task.Run(() => null);'; Replace = 'await Task.CompletedTask;' }
    )
    
    "clients\GGs.Desktop\Views\ProfileArchitectView.xaml.cs" = @(
        @{ Find = 'public event PropertyChangedEventHandler PropertyChanged;'; Replace = 'public event PropertyChangedEventHandler? PropertyChanged;' }
        @{ Find = 'public string Name { get; set; }'; Replace = 'public string Name { get; set; } = string.Empty;' }
        @{ Find = 'public string CurrentOperation { get; set; }'; Replace = 'public string CurrentOperation { get; set; } = string.Empty;' }
        @{ Find = 'private ProfileViewModel _selectedProfile;'; Replace = 'private ProfileViewModel? _selectedProfile;' }
        @{ Find = 'private async Task<ProfileViewModel> CreateProfileViewModel'; Replace = 'private Task<ProfileViewModel?> CreateProfileViewModel' }
        @{ Find = 'return null;'; Replace = 'return Task.FromResult<ProfileViewModel?>(null);' }
    )
    
    "clients\GGs.Desktop\Views\DashboardView.xaml.cs" = @(
        @{ Find = '_apiClient.'; Replace = '_apiClient?.'; ReplaceAll = $true }
        @{ Find = 'entitlements.'; Replace = 'entitlements?.'; ReplaceAll = $true }
    )
    
    "clients\GGs.Desktop\MainWindow.xaml.cs" = @(
        @{ Find = '_eli.Answer('; Replace = '_eli.AskQuestionAsync(' }
    )
    
    "clients\GGs.Desktop\Views\ModernMainWindow.xaml.cs" = @(
        @{ Find = '_eli.Answer('; Replace = '_eli.AskQuestionAsync(' }
    )
    
    "clients\GGs.Desktop\App.xaml.cs" = @(
        @{ Find = 'Directory.CreateDirectory(Path.GetDirectoryName(logPath));'; Replace = 'Directory.CreateDirectory(Path.GetDirectoryName(logPath) ?? ".");' }
        @{ Find = 'AppLogger.LogError("CRITICAL ERROR: Application startup failed", ex);'; Replace = 'AppLogger.LogError("CRITICAL ERROR: Application startup failed", ex!);' }
        @{ Find = 'Directory.CreateDirectory(Path.GetDirectoryName(bootstrapPath));'; Replace = 'Directory.CreateDirectory(Path.GetDirectoryName(bootstrapPath) ?? ".");' }
    )
    
    "clients\GGs.Desktop\Services\AccessibilityService.cs" = @(
        @{ Find = 'Application.Current.MainWindow = null;'; Replace = 'Application.Current.MainWindow = null!;' }
    )
    
    "clients\GGs.Desktop\ViewModels\Admin\CloudProfilesViewModel.cs" = @(
        @{ Find = 'public ICommand DownloadProfileCommand { get; set; }'; Replace = 'public ICommand DownloadProfileCommand { get; set; } = null!;' }
        @{ Find = 'public ICommand VerifySignatureCommand { get; set; }'; Replace = 'public ICommand VerifySignatureCommand { get; set; } = null!;' }
        @{ Find = 'public ICommand ApproveProfileCommand { get; set; }'; Replace = 'public ICommand ApproveProfileCommand { get; set; } = null!;' }
    )
    
    "clients\GGs.Desktop\ViewModels\Analytics\AuditSearchViewModel.cs" = @(
        @{ Find = 'SearchAuditLogsAsync(client, SearchQuery,'; Replace = 'SearchAuditLogsAsync(client, SearchQuery ?? string.Empty,' }
    )
    
    "clients\GGs.Desktop\Views\Controls\SystemTweaksPanel.xaml.cs" = @(
        @{ Find = 'private void UpdateRealTimeInformation(object sender, EventArgs e)'; Replace = 'private void UpdateRealTimeInformation(object? sender, EventArgs e)' }
        @{ Find = 'private CancellationTokenSource _cancellationTokenSource;'; Replace = 'private CancellationTokenSource? _cancellationTokenSource;' }
    )
    
    "clients\GGs.Desktop\Views\Controls\AnimatedProgressBar.xaml.cs" = @(
        @{ Find = 'private DispatcherTimer _iconRotationTimer;'; Replace = 'private DispatcherTimer? _iconRotationTimer;' }
        @{ Find = 'private Storyboard _currentAnimation;'; Replace = 'private Storyboard? _currentAnimation;' }
    )
    
    "clients\GGs.Desktop\Views\ErrorLogViewer.xaml.cs" = @(
        @{ Find = 'private FileSystemWatcher _watcher;'; Replace = 'private FileSystemWatcher? _watcher;' }
        @{ Find = 'catch (Exception ex)'; Replace = 'catch (Exception)' }
    )
    
    "clients\GGs.Desktop\Services\AdminHubClient.cs" = @(
        @{ Find = 'public event EventHandler<AuditLogAddedEventArgs> AuditAdded;'; Replace = '#pragma warning disable CS0067\n        public event EventHandler<AuditLogAddedEventArgs>? AuditAdded;\n        #pragma warning restore CS0067' }
    )
    
    "clients\GGs.Desktop\Services\SystemIntelligenceService.cs" = @(
        @{ Find = 'private async Task ScanRegistryAsync('; Replace = 'private Task ScanRegistryAsync(' }
        @{ Find = 'private async Task AnalyzeTweaksAsync('; Replace = 'private Task AnalyzeTweaksAsync(' }
        @{ Find = 'private async Task<List<DetectedTweak>> DetectAdvancedTweaksAsync('; Replace = 'private Task<List<DetectedTweak>> DetectAdvancedTweaksAsync(' }
    )
}

foreach ($file in $fileFixes.Keys) {
    $fullPath = Join-Path $projectRoot $file
    if (-not (Test-Path $fullPath)) {
        Write-Host "SKIP: File not found: $file" -ForegroundColor DarkGray
        continue
    }
    
    Write-Host "Processing: $file" -ForegroundColor Yellow
    $content = Get-Content $fullPath -Raw -Encoding UTF8
    $modified = $false
    
    foreach ($fix in $fileFixes[$file]) {
        if ($fix.ReplaceAll) {
            if ($content -match [regex]::Escape($fix.Find)) {
                $content = $content -replace [regex]::Escape($fix.Find), $fix.Replace
                $modified = $true
                Write-Host "  [OK] Applied fix (all occurrences): $($fix.Find.Substring(0, [Math]::Min(50, $fix.Find.Length)))..." -ForegroundColor Green
            }
        }
        else {
            if ($content.Contains($fix.Find)) {
                $content = $content.Replace($fix.Find, $fix.Replace)
                $modified = $true
                Write-Host "  [OK] Applied fix: $($fix.Find.Substring(0, [Math]::Min(50, $fix.Find.Length)))..." -ForegroundColor Green
            }
        }
    }
    
    if ($modified) {
        Set-Content $fullPath -Value $content -Encoding UTF8 -NoNewline
        $fixCount++
    }
}

Write-Host ""
Write-Host "=== Summary ===" -ForegroundColor Cyan
Write-Host "Files modified: $fixCount" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Run: dotnet build GGs.sln -c Release" -ForegroundColor White
Write-Host "2. Fix any remaining errors" -ForegroundColor White
Write-Host "3. Commit and push changes" -ForegroundColor White
