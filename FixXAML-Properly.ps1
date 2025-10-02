# Fix XAML properly - surgical approach
$ErrorActionPreference = "Stop"

Write-Host "Fixing ErrorLogViewer XAML..." -ForegroundColor Cyan

$xamlPath = "tools\GGs.ErrorLogViewer\Views\MainWindow.xaml"
$content = Get-Content $xamlPath -Raw

# Fix 1: Update window size
$content = $content -replace 'Width="1600"', 'Width="1200"'
$content = $content -replace 'MinWidth="1280"', 'MinWidth="1024"'
$content = $content -replace 'MinHeight="720"', 'MinHeight="600"'

# Fix 2: Wrap existing Main Content Grid (Grid.Row="2") with visibility for Logs view
$pattern = '(<!-- Main Content -->[\r\n\s]+<Grid Grid\.Row="2" Margin="0,12,0,0">)'
$replacement = '$1' + [Environment]::NewLine + '            <!-- Logs View (existing) -->' + [Environment]::NewLine + '            <Grid Visibility="{Binding ActiveView, Converter={StaticResource StringEqualsToVisibilityConverter}, ConverterParameter=Logs}">'
$content = $content -replace $pattern, $replacement

# Fix 3: Close the Logs view Grid before Status Bar and add new views
$statusPattern = '        </Grid>[\r\n\s]+<!-- Status Bar -->'
$newViews = @'
            </Grid>
        
            <!-- Analytics View -->
            <Grid Grid.Row="2" Margin="16" Visibility="{Binding ActiveView, Converter={StaticResource StringEqualsToVisibilityConverter}, ConverterParameter=Analytics}">
                <ScrollViewer>
                    <StackPanel>
                        <TextBlock Text="📊 Analytics Dashboard" FontSize="24" FontWeight="Bold" Margin="0,0,0,20"/>
                        <Grid Margin="0,0,0,20">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="*"/>
                            </Grid.ColumnDefinitions>
                            <Border Grid.Column="0" Background="{DynamicResource SystemControlBackgroundAltMediumBrush}" Padding="16" Margin="0,0,4,0" CornerRadius="8">
                                <StackPanel>
                                    <TextBlock Text="Total Logs" Opacity="0.7" FontSize="12"/>
                                    <TextBlock Text="{Binding CurrentStatistics.TotalCount}" FontSize="28" FontWeight="Bold"/>
                                </StackPanel>
                            </Border>
                            <Border Grid.Column="1" Background="{DynamicResource SystemControlBackgroundAltMediumBrush}" Padding="16" Margin="4,0" CornerRadius="8">
                                <StackPanel>
                                    <TextBlock Text="Errors" Opacity="0.7" FontSize="12"/>
                                    <TextBlock Text="{Binding CurrentStatistics.ErrorCount}" FontSize="28" FontWeight="Bold" Foreground="Red"/>
                                </StackPanel>
                            </Border>
                            <Border Grid.Column="2" Background="{DynamicResource SystemControlBackgroundAltMediumBrush}" Padding="16" Margin="4,0" CornerRadius="8">
                                <StackPanel>
                                    <TextBlock Text="Warnings" Opacity="0.7" FontSize="12"/>
                                    <TextBlock Text="{Binding CurrentStatistics.WarningCount}" FontSize="28" FontWeight="Bold" Foreground="Orange"/>
                                </StackPanel>
                            </Border>
                            <Border Grid.Column="3" Background="{DynamicResource SystemControlBackgroundAltMediumBrush}" Padding="16" Margin="4,0,0,0" CornerRadius="8">
                                <StackPanel>
                                    <TextBlock Text="Info" Opacity="0.7" FontSize="12"/>
                                    <TextBlock Text="{Binding CurrentStatistics.InformationCount}" FontSize="28" FontWeight="Bold" Foreground="DodgerBlue"/>
                                </StackPanel>
                            </Border>
                        </Grid>
                        <StackPanel Orientation="Horizontal" Margin="0,20,0,0">
                            <Button Content="Refresh Analytics" Command="{Binding RefreshAnalyticsCommand}" Margin="0,0,8,0" Padding="16,8"/>
                            <Button Content="Find Anomalies" Command="{Binding FindAnomaliesCommand}" Margin="0,0,8,0" Padding="16,8"/>
                            <Button Content="Export Report" Command="{Binding ExportAnalyticsCommand}" Padding="16,8"/>
                        </StackPanel>
                    </StackPanel>
                </ScrollViewer>
            </Grid>
            
            <!-- Export View -->
            <Grid Grid.Row="2" Margin="16" Visibility="{Binding ActiveView, Converter={StaticResource StringEqualsToVisibilityConverter}, ConverterParameter=Export}">
                <ScrollViewer>
                    <StackPanel MaxWidth="700">
                        <TextBlock Text="📁 Export &amp; Import" FontSize="24" FontWeight="Bold" Margin="0,0,0,20"/>
                        <GroupBox Header="Export Options" Margin="0,0,0,16" Padding="16">
                            <StackPanel>
                                <Button Content="📄 Export to PDF" Command="{Binding ExportToPdfCommand}" HorizontalAlignment="Stretch" Margin="0,0,0,8" Padding="12,8"/>
                                <Button Content="📝 Export to Markdown" Command="{Binding ExportToMarkdownCommand}" HorizontalAlignment="Stretch" Margin="0,0,0,8" Padding="12,8"/>
                                <Button Content="⏱ Export Last 24 Hours" Command="{Binding ExportLast24HoursCommand}" HorizontalAlignment="Stretch" Padding="12,8"/>
                            </StackPanel>
                        </GroupBox>
                        <GroupBox Header="Import Options" Padding="16">
                            <StackPanel>
                                <Button Content="📋 Import Windows Event Log" Command="{Binding ImportWindowsEventLogCommand}" HorizontalAlignment="Stretch" Margin="0,0,0,8" Padding="12,8"/>
                                <Button Content="📄 Import Syslog File" Command="{Binding ImportSyslogCommand}" HorizontalAlignment="Stretch" Margin="0,0,0,8" Padding="12,8"/>
                                <Button Content="🔧 Import Custom Format" Command="{Binding ImportCustomFormatCommand}" HorizontalAlignment="Stretch" Padding="12,8"/>
                            </StackPanel>
                        </GroupBox>
                    </StackPanel>
                </ScrollViewer>
            </Grid>
            
            <!-- Settings View -->
            <Grid Grid.Row="2" Margin="16" Visibility="{Binding ActiveView, Converter={StaticResource StringEqualsToVisibilityConverter}, ConverterParameter=Settings}">
                <ScrollViewer>
                    <StackPanel MaxWidth="700">
                        <TextBlock Text="⚙️ Settings" FontSize="24" FontWeight="Bold" Margin="0,0,0,20"/>
                        <GroupBox Header="Display Settings" Padding="16" Margin="0,0,0,16">
                            <StackPanel>
                                <TextBlock Text="Font Size" Margin="0,0,0,4"/>
                                <Slider Minimum="9" Maximum="24" Value="{Binding LogFontSize}" TickFrequency="1" IsSnapToTickEnabled="True"/>
                                <CheckBox Content="Auto-scroll new logs" IsChecked="{Binding AutoScroll}" Margin="0,12,0,0"/>
                                <CheckBox Content="Show details pane" IsChecked="{Binding IsDetailsPaneVisible}" Margin="0,8,0,0"/>
                            </StackPanel>
                        </GroupBox>
                    </StackPanel>
                </ScrollViewer>
            </Grid>
        </Grid>

        <!-- Status Bar -->'@

$content = $content -replace $statusPattern, $newViews

# Save
Set-Content $xamlPath -Value $content -NoNewline

# Build
Write-Host "Building..." -ForegroundColor Yellow
Push-Location "tools\GGs.ErrorLogViewer"
dotnet build -c Release --nologo
$success = $LASTEXITCODE -eq 0
Pop-Location

if ($success) {
    Write-Host "✓ Success! Launch the app now." -ForegroundColor Green
} else {
    Write-Host "✗ Build failed, restoring backup..." -ForegroundColor Red
    Copy-Item "$xamlPath.backup" $xamlPath -Force
}
