# GGs Enterprise Suite - User Guide

## Welcome to GGs Enterprise

GGs Enterprise is a comprehensive suite of professional-grade tools for gaming optimization and system monitoring. This guide covers the essential features, navigation, and key tasks for both applications.

---

## Table of Contents

1. [Getting Started](#getting-started)
2. [GGs Desktop - Gaming Optimization Suite](#ggs-desktop---gaming-optimization-suite)
3. [GGs Error Log Viewer - Enterprise Monitoring](#ggs-error-log-viewer---enterprise-monitoring)
4. [Themes & Customization](#themes--customization)
5. [Keyboard Navigation](#keyboard-navigation)
6. [Launchers & Build Tools](#launchers--build-tools)
7. [Troubleshooting](#troubleshooting)

---

## Getting Started

### System Requirements
- **OS**: Windows 10 (1809+) or Windows 11
- **.NET**: .NET 9.0 Runtime or SDK
- **RAM**: 4 GB minimum, 8 GB recommended
- **Display**: 1920x1080 minimum resolution

### Installation
1. Download the latest release from the releases page
2. Extract to your preferred location
3. Run the appropriate launcher (see [Launchers](#launchers--build-tools))

### First Launch
Both applications feature a welcoming first-run experience:
- **GGs Desktop**: Full-screen welcome overlay with initialization progress
- **GGs Error Log Viewer**: Quick actions banner with helpful shortcuts

---

## GGs Desktop - Gaming Optimization Suite

### Overview
GGs Desktop is your command center for gaming optimization, system monitoring, and performance tuning.

### Main Features

#### 1. Dashboard
**Location**: Default view on launch

**Key Elements**:
- **Health Cards**: Real-time CPU, GPU, Memory, and Network metrics
  - CPU Usage: Shows percentage and clock speed
  - GPU Usage: Shows percentage and GPU status
  - Memory: Shows used GB and free GB
  - Network Latency: Shows ping in milliseconds

- **EliBot Assistant**: AI-powered help system
  - Type your question in the text box
  - Click "Ask" to get instant answers
  - Covers optimization tips, troubleshooting, and system info

- **Quick Actions**: One-click optimization modes
  - 🎮 **Game Mode**: Optimize for gaming performance
  - ⚡ **Performance Boost**: Maximum performance mode
  - 🧹 **System Clean**: Free up system resources
  - 🔇 **Silent Mode**: Quiet operation for streaming

- **Performance Monitor**: Real-time performance graph (coming soon)

#### 2. Optimization
**Location**: Navigation → Optimization

**Features**:
- Process priority management
- Background service optimization
- Startup program control
- Disk cleanup utilities

#### 3. Network
**Location**: Navigation → Network

**Features**:
- Network adapter optimization
- DNS configuration
- Latency monitoring
- Bandwidth management

#### 4. Monitoring
**Location**: Navigation → Monitoring

**Features**:
- Real-time system metrics
- Temperature monitoring
- Resource usage graphs
- Alert configuration

#### 5. Profiles
**Location**: Navigation → Profiles

**Features**:
- Cloud profile management
- Profile synchronization
- API token configuration
- Profile import/export

#### 6. System Intelligence
**Location**: Navigation → System Intelligence

**Tabs**:
- **System Intelligence**: AI-powered optimization recommendations
- **Profile Architect**: Custom profile creation
- **Community Hub**: Share and download community profiles

#### 7. Notifications
**Location**: Navigation → Notifications

**Features**:
- System alerts
- Optimization suggestions
- Update notifications
- Badge counter in title bar

#### 8. Settings
**Location**: Navigation → Settings

**Sections**:
- **Appearance**: Theme, font size, accent colors
- **Server**: Base URL configuration
- **Updates**: Update channel and version info
- **Secrets & Configuration**: API tokens, settings import/export
- **Privacy & Diagnostics**: Crash reporting, logs folder
- **Deep Optimization**: Windows service management (admin required)

---

## GGs Error Log Viewer - Enterprise Monitoring

### Overview
GGs Error Log Viewer is an enterprise-grade log monitoring and analysis tool with real-time intelligence.

### Main Features

#### 1. Live Logs
**Location**: Default view on launch

**Key Elements**:
- **Hero Section**: Live monitoring status and action buttons
  - Start/Stop Monitoring
  - Refresh logs
  - Clear logs
  - Export logs

- **Filter Bar**: Powerful filtering and search
  - **Search Box**: Full-text search with regex support
  - **Log Level Filter**: All, Verbose, Debug, Info, Warning, Error, Critical
  - **Source Filter**: Filter by log source
  - **Toggles**:
    - Regex: Enable regex pattern matching
    - Smart Filter: Deduplicate similar entries
    - Auto Scroll: Auto-scroll to new entries
    - Raw: Toggle raw/compact message view
  - **Font Size Slider**: Adjust log font size (10-20pt)
  - **Entry Counter**: Shows filtered/total entry count

- **DataGrid**: Enterprise-grade log display
  - **Color-Coded Rows**: 4px left accent bar by log level
    - Red: Error/Critical
    - Orange: Warning
    - Cyan: Information
    - Green: Success
    - Gray: Debug/Trace
  - **Columns**: Timestamp, Level, Source, Message, (optional) File Path
  - **Details Expander**: Click row to expand full details
  - **Context Menu**: Copy, export, bookmark actions
  - **Virtualization**: Handles 50,000+ entries smoothly

#### 2. Analytics
**Location**: Navigation → Analytics

**Key Elements**:
- **Stat Cards**: Health Score, Error Count, Warning Count, Event Rate
- **Top Sources**: Progress bars showing log volume by source
- **Log Level Distribution**: Progress bars showing level distribution
- **Action Buttons**:
  - Refresh Analytics: Update statistics
  - Detect Anomalies: Find unusual patterns

#### 3. Bookmarks
**Location**: Navigation → Bookmarks

**Features**:
- **Add Bookmark**: Save important log entries for later review
- **Bookmark Cards**: Show title, message, metadata, and tags
- **Actions**:
  - Go To: Navigate to bookmarked log entry
  - Remove: Delete bookmark

**Usage**:
1. Select a log entry in Live Logs view
2. Navigate to Bookmarks view
3. Click "Add Bookmark"
4. Bookmark appears in list with full context

#### 4. Smart Alerts
**Location**: Navigation → Smart Alerts

**Features**:
- **Triggered Alerts**: Active alerts requiring attention
  - Alert name and pattern
  - Triggered timestamp
  - Match count
  - Acknowledge button

- **Configured Alert Rules**: Manage alert definitions
  - Alert name and pattern
  - Enable/Disable toggle
  - Delete button

**Usage**:
1. Click "Create Alert"
2. Enter alert name and pattern (regex supported)
3. Alert monitors logs in real-time
4. Triggered alerts appear in Triggered Alerts section
5. Click "Acknowledge" to dismiss

#### 5. Compare Runs
**Location**: Navigation → Compare Runs
**Status**: Coming soon

#### 6. Exports
**Location**: Navigation → Exports
**Status**: Coming soon

#### 7. Settings
**Location**: Navigation → Settings

**Features**:
- Log file paths
- Monitoring intervals
- Export preferences
- Theme settings

### Exporting Logs

**CSV Export**:
1. Apply desired filters in Live Logs view
2. Click "Export" button
3. Select "Export to CSV"
4. Choose save location
5. CSV file created with filtered entries

**JSON Export**:
1. Apply desired filters in Live Logs view
2. Click "Export" button
3. Select "Export to JSON"
4. Choose save location
5. JSON file created with pretty-print formatting

**Export Features**:
- Timestamped filenames: `logs_export_yyyyMMdd_HHmmss.csv`
- Exports only filtered entries (respects current filters)
- Success/error feedback via message box
- Detailed logging for diagnostics

---

## Themes & Customization

### Available Themes

Both applications support 5 professional themes:

1. **Midnight Cyan** (Default)
   - Dark background with cyan accents
   - Best for low-light environments
   - High contrast for readability

2. **Vapor Purple**
   - Dark background with purple accents
   - Retro-futuristic aesthetic
   - Easy on the eyes

3. **Tactical Green**
   - Dark background with green accents
   - Military-inspired design
   - Excellent for focus

4. **Carbon Minimal**
   - Ultra-dark background with subtle accents
   - Minimalist design
   - Maximum immersion

5. **Lumen Light**
   - Light background with dark text
   - Best for bright environments
   - High contrast for accessibility

### Changing Themes

**GGs Desktop**:
1. Click the theme toggle button in the title bar (🌙/☀️ icon)
2. Theme cycles through all 5 options
3. Theme persists across restarts

**GGs Error Log Viewer**:
1. Navigate to Settings view
2. Select theme from dropdown
3. Theme applies immediately
4. Theme persists across restarts

### Custom Accent Colors

**GGs Desktop Only**:
1. Navigate to Settings → Appearance
2. Enter hex color codes for:
   - Accent Primary (e.g., #00FFFF)
   - Accent Secondary (e.g., #9D4EDD)
3. Colors update in real-time
4. Preview squares show current colors

### Font Size Adjustment

**GGs Desktop**:
- Settings → Appearance → Font Size slider (10-22pt)

**GGs Error Log Viewer**:
- Filter Bar → Font Size slider (10-20pt)
- Applies to log entries only

---

## Keyboard Navigation

Both applications are fully keyboard-accessible for productivity and accessibility.

### Universal Shortcuts

- **Tab**: Navigate forward through controls
- **Shift+Tab**: Navigate backward through controls
- **Enter**: Activate button or select item
- **Space**: Toggle checkbox or toggle button
- **Escape**: Close dialog or cancel action
- **Arrow Keys**: Navigate within lists, grids, and dropdowns

### GGs Desktop Shortcuts

**Window Controls**:
- Tab to Notifications button → Enter to open
- Tab to Theme toggle → Enter to change theme
- Tab to Minimize → Enter to minimize
- Tab to Maximize → Enter to maximize/restore
- Tab to Close → Enter to close (or Escape to cancel)

**Navigation**:
- Tab to navigation rail
- Use Arrow Keys to move between views
- Enter to select view

**Dashboard**:
- Tab to Quick Optimize button
- Tab to EliBot question box
- Tab to Quick Action buttons

### GGs Error Log Viewer Shortcuts

**Filter Bar**:
- Tab to Search box → Type to search
- Tab to Level filter → Arrow Keys to select
- Tab to Source filter → Arrow Keys to select
- Tab to toggle buttons → Space to toggle

**Action Buttons**:
- Tab to Start/Stop/Refresh/Clear/Export buttons
- Enter to activate

**Navigation**:
- Tab to navigation panel
- Use Arrow Keys to move between views
- Enter to select view

**DataGrid**:
- Tab to DataGrid
- Arrow Keys to navigate rows and columns
- Enter to expand/collapse details
- Context Menu key for actions

### Accessibility Features

- **Screen Reader Support**: All controls have descriptive names
- **High Contrast Mode**: Compatible with Windows High Contrast themes
- **Focus Visuals**: Clear focus indicators on all interactive elements
- **Keyboard-Only Operation**: All features accessible without mouse

---

## Launchers & Build Tools

### Available Launchers

The GGs Enterprise suite includes 3 production-ready launchers:

#### 1. Launch-ErrorLogViewer.bat
**Purpose**: Build and launch the Error Log Viewer

**Usage**:
```bash
Launch-ErrorLogViewer.bat [--release] [--no-restore] [--no-launch]
```

**Flags**:
- `--release`: Build in Release configuration (default: Debug)
- `--no-restore`: Skip dependency restore (faster if already restored)
- `--no-launch`: Build only, don't launch application

**Examples**:
```bash
Launch-ErrorLogViewer.bat                    # Debug build with launch
Launch-ErrorLogViewer.bat --release          # Release build with launch
Launch-ErrorLogViewer.bat --no-launch        # Build only
```

#### 2. Launch-Desktop.bat
**Purpose**: Build and launch the Desktop application

**Usage**:
```bash
Launch-Desktop.bat [--release] [--no-restore] [--no-launch] [--with-server]
```

**Flags**:
- `--release`: Build in Release configuration
- `--no-restore`: Skip dependency restore
- `--no-launch`: Build only, don't launch
- `--with-server`: Auto-start local server on localhost:5000

**Examples**:
```bash
Launch-Desktop.bat                           # Debug build with launch
Launch-Desktop.bat --with-server             # Debug with local server
Launch-Desktop.bat --release --with-server   # Release with server
```

#### 3. Launch-All.bat
**Purpose**: Build and launch entire suite

**Usage**:
```bash
Launch-All.bat [--release] [--no-restore] [--skip-tests] [--no-launch]
```

**Flags**:
- `--release`: Build in Release configuration
- `--no-restore`: Skip dependency restore
- `--skip-tests`: Skip unit test execution
- `--no-launch`: Build only, don't launch applications

**Examples**:
```bash
Launch-All.bat                               # Full build, test, and launch
Launch-All.bat --skip-tests                  # Skip tests for faster launch
Launch-All.bat --release                     # Release build with tests
```

### Launcher Features

**All launchers include**:
- Clean → Build → Verify → Launch workflow
- Detailed logging to `launcher-logs/` folder
- Timestamped log files
- Clear error messages
- Non-zero exit codes on failure
- Crash-proof error handling

**Failure Modes Handled**:
- Missing .NET SDK → Clear error with download link
- Build errors → Log file with full details
- Missing executable → Path verification error
- Launch failures → Detailed error message

### Quick Test Suite

**Run-QuickTests.bat**: Automated test suite for quick verification

**Usage**:
```bash
.\Run-QuickTests.bat
```

**Tests Performed**:
1. Check .NET SDK presence and version
2. Build solution (Debug configuration)
3. Build solution (Release configuration)
4. Verify Desktop executable exists
5. Verify ErrorLogViewer executable exists
6. Run unit tests
7. Scan for placeholders (TODO/FIXME/XXX)
8. Check theme files (all 5 palettes)

**Output**:
- Real-time test progress
- Pass/fail status for each test
- Summary with total/passed/failed counts
- Detailed log file in `launcher-logs/`
- Exit code 0 on success, 1 on failure

---

## Troubleshooting

### Common Issues

#### Application Won't Launch
**Symptoms**: Double-clicking exe does nothing or shows error

**Solutions**:
1. Verify .NET 9.0 Runtime is installed
   - Download from: https://dotnet.microsoft.com/download
2. Check Windows Event Viewer for crash details
3. Run from command line to see error messages
4. Check `launcher-logs/` for detailed error logs

#### Theme Not Persisting
**Symptoms**: Theme resets to default on restart

**Solutions**:
1. Check settings file permissions
   - Desktop: `%LocalAppData%\GGs\settings.json`
   - ErrorLogViewer: `%LocalAppData%\GGs.ErrorLogViewer\settings.json`
2. Ensure application has write permissions
3. Try running as administrator (not recommended for regular use)

#### Logs Not Appearing
**Symptoms**: ErrorLogViewer shows no logs

**Solutions**:
1. Click "Start Monitoring" button
2. Check log file paths in Settings
3. Verify log files exist and are readable
4. Try "Import Sample Log" for testing
5. Check file permissions on log directories

#### High CPU/Memory Usage
**Symptoms**: Application uses excessive resources

**Solutions**:
1. ErrorLogViewer: Reduce log file size or apply filters
2. Desktop: Disable real-time monitoring if not needed
3. Close unused views/tabs
4. Restart application to clear memory
5. Check for background processes

#### Keyboard Navigation Not Working
**Symptoms**: Tab key doesn't move focus

**Solutions**:
1. Click inside the application window first
2. Check if a modal dialog is open
3. Try Shift+Tab to navigate backward
4. Restart application if focus is stuck

### Getting Help

**Documentation**:
- User Guide: `USER_GUIDE.md` (this file)
- Test Checklist: `TEST_CHECKLIST.md`
- Keyboard Navigation: `KEYBOARD_NAVIGATION_TEST.md`
- Root-Cause Fixes: `ROOT_CAUSE_FIX_LOG.md`

**Logs**:
- Launcher logs: `launcher-logs/` folder
- Application logs: Check Settings for log file paths
- Crash reports: `%LocalAppData%\GGs\CrashReports\`

**Support**:
- Check GitHub Issues for known problems
- Submit bug reports with log files attached
- Include steps to reproduce the issue

---

## Appendix

### File Locations

**GGs Desktop**:
- Executable: `clients\GGs.Desktop\bin\[Debug|Release]\net9.0-windows\GGs.Desktop.exe`
- Settings: `%LocalAppData%\GGs\settings.json`
- Logs: `%LocalAppData%\GGs\logs\`
- Crash Reports: `%LocalAppData%\GGs\CrashReports\`

**GGs Error Log Viewer**:
- Executable: `tools\GGs.ErrorLogViewer\bin\[Debug|Release]\net9.0-windows\GGs.ErrorLogViewer.exe`
- Settings: `%LocalAppData%\GGs.ErrorLogViewer\settings.json`
- Logs: `%LocalAppData%\GGs.ErrorLogViewer\logs\`

**Launcher Logs**:
- Location: `launcher-logs/` in repository root
- Format: `Launch-[App]_yyyyMMdd_HHmmss.log`

### Version Information

**Current Version**: 1.0.0
**Build Date**: 2025-10-02
**.NET Version**: 9.0
**Supported OS**: Windows 10 (1809+), Windows 11

---

**End of User Guide**

For the latest updates and documentation, visit the GitHub repository.

