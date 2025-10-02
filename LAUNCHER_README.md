# GGs Enterprise Suite - Launcher Guide

## 📋 Overview

The GGs Enterprise Suite includes multiple advanced launchers for different use cases. Each launcher is production-ready with comprehensive error handling, logging, and process monitoring.

---

## 🚀 Available Launchers

### 1. **Start.GGs.bat** - Desktop Only Launcher
**Purpose**: Launch only the GGs Desktop application

**Features**:
- Advanced build verification
- Single-instance protection
- Memory monitoring
- Optional ErrorLogViewer integration
- Comprehensive error handling

**Usage**:
```batch
# Basic launch
Start.GGs.bat

# Launch with ErrorLogViewer
Start.GGs.bat --with-logviewer

# Launch with custom log directory
Start.GGs.bat --log-dir "C:\Logs\MyApp"

# Debug mode
Start.GGs.bat --debug --verbose
```

**Command Line Options**:
- `--debug` - Launch in Debug mode (default: Release)
- `--skip-build` - Skip building the application
- `--with-logviewer` - Also launch ErrorLogViewer
- `--log-dir DIR` - Launch with ErrorLogViewer using custom log directory
- `--verbose` - Enable verbose output
- `--help` - Show help message

---

### 2. **Start.ErrorLogViewer.bat** - Log Viewer Only Launcher
**Purpose**: Launch only the ErrorLogViewer application (standalone monitoring tool)

**Features**:
- Independent operation (no desktop app required)
- Crash-proof design with proper disposal
- Auto-start monitoring capability
- Custom log directory support
- Build mode selection (Debug/Release)

**Usage**:
```batch
# Basic launch
Start.ErrorLogViewer.bat

# Custom log directory
Start.ErrorLogViewer.bat --log-dir "C:\Projects\MyApp\logs"

# No auto-start
Start.ErrorLogViewer.bat --no-auto-start

# Debug mode with verbose output
Start.ErrorLogViewer.bat --debug --verbose
```

**Command Line Options**:
- `--log-dir DIR` - Specify custom log directory
- `--no-auto-start` - Don't auto-start monitoring
- `--debug` - Launch in Debug mode (default: Release)
- `--skip-build` - Skip building the application
- `--verbose` - Enable verbose output
- `--help` - Show help message

---

### 3. **Start.Both.bat** - Unified Launcher
**Purpose**: Launch both GGs Desktop and ErrorLogViewer together (recommended for full suite)

**Features**:
- Coordinated launch of both applications
- Staggered startup to prevent conflicts
- Process monitoring with auto-exit detection
- PowerShell launcher integration
- Fallback batch mode if PowerShell unavailable

**Usage**:
```batch
# Launch both applications
Start.Both.bat

# Custom log directory for ErrorLogViewer
Start.Both.bat --log-dir "C:\Logs"

# Launch with process monitoring
Start.Both.bat --monitor

# Skip one component
Start.Both.bat --skip-logviewer  # Desktop only
Start.Both.bat --skip-desktop    # LogViewer only

# Debug mode
Start.Both.bat --debug --verbose
```

**Command Line Options**:
- `--log-dir DIR` - Specify custom log directory for ErrorLogViewer
- `--debug` - Launch in Debug mode (default: Release)
- `--skip-build` - Skip building applications
- `--skip-desktop` - Don't launch GGs Desktop
- `--skip-logviewer` - Don't launch ErrorLogViewer
- `--monitor` - Monitor processes until they exit
- `--verbose` - Enable verbose output
- `--help` - Show help message

---

### 4. **GGsLauncher.ps1** - PowerShell Enterprise Launcher
**Purpose**: Advanced PowerShell-based launcher with comprehensive logging and monitoring

**Features**:
- Enterprise-grade error handling
- Detailed logging to file (launcher-logs/)
- Process health monitoring
- Graceful shutdown handling
- Build verification and dependency checks

**Usage**:
```powershell
# Launch both applications
.\GGsLauncher.ps1

# Launch only Desktop
.\GGsLauncher.ps1 -Desktop

# Launch only LogViewer
.\GGsLauncher.ps1 -LogViewer

# Custom log directory
.\GGsLauncher.ps1 -LogDirectory "C:\Logs"

# Debug mode with verbose logging
.\GGsLauncher.ps1 -Configuration Debug -VerboseLogging

# Skip build
.\GGsLauncher.ps1 -SkipBuild
```

**Parameters**:
- `-Desktop` - Launch only GGs Desktop
- `-LogViewer` - Launch only ErrorLogViewer
- `-NoLogViewer` - Launch Desktop without LogViewer
- `-LogDirectory` - Specify custom log directory
- `-Configuration` - Build configuration (Debug/Release, default: Release)
- `-SkipBuild` - Skip building applications
- `-VerboseLogging` - Enable verbose logging

---

## 🛡️ Stability & Crash Prevention

### ErrorLogViewer Independent Stability

The ErrorLogViewer has been specifically hardened for standalone operation:

**Memory Management**:
- Proper IDisposable implementation
- Automatic resource cleanup on shutdown
- Event handler unsubscription
- Collection clearing

**Error Handling**:
- Try-catch blocks in all critical paths
- Null-safe operations throughout
- Graceful degradation on service failures
- Comprehensive logging for diagnostics

**Process Lifecycle**:
- Clean startup with dependency verification
- Monitored runtime with health checks
- Graceful shutdown with resource disposal
- No orphaned processes or file handles

---

## 🧪 Testing

### Comprehensive Test Suite

Run the test suite to verify ErrorLogViewer stability:

```batch
cd tools\GGs.ErrorLogViewer
Test.ErrorLogViewer.bat
```

**Tests Included**:
1. Build verification
2. Executable existence
3. Dependencies verification
4. Configuration file check
5. Quick launch test
6. Short-term stability test (5 seconds)
7. Test log directory creation
8. Test log file generation
9. Memory usage tracking
10. Graceful shutdown

**Expected Results**:
- ✓ All 10 tests should pass
- ✓ 100% success rate
- ✓ No crashes or errors
- ✓ Clean shutdown

---

## 📁 File Structure

```
GGs/
├── Start.GGs.bat                    # Desktop launcher
├── Start.ErrorLogViewer.bat         # LogViewer launcher  
├── Start.Both.bat                   # Unified launcher
├── GGsLauncher.ps1                  # PowerShell enterprise launcher
├── LAUNCHER_README.md               # This file
│
├── clients/
│   └── GGs.Desktop/
│       └── bin/Release/net9.0-windows/
│           └── GGs.Desktop.exe
│
└── tools/
    └── GGs.ErrorLogViewer/
        ├── bin/Release/net9.0-windows/
        │   └── GGs.ErrorLogViewer.exe
        └── Test.ErrorLogViewer.bat  # Test suite
```

---

## 🎯 Recommended Usage Scenarios

| Scenario | Recommended Launcher | Command |
|----------|---------------------|---------|
| Full development suite | Start.Both.bat | `Start.Both.bat` |
| Desktop app only | Start.GGs.bat | `Start.GGs.bat` |
| Log monitoring only | Start.ErrorLogViewer.bat | `Start.ErrorLogViewer.bat --log-dir "C:\Logs"` |
| Production deployment | GGsLauncher.ps1 | `.\GGsLauncher.ps1 -Configuration Release` |
| Testing/Debugging | Start.Both.bat | `Start.Both.bat --debug --monitor` |
| Custom log analysis | Start.ErrorLogViewer.bat | `Start.ErrorLogViewer.bat --log-dir "C:\CustomLogs"` |

---

## ⚙️ Configuration

### Environment Variables

The launchers support the following environment configurations:

- **DOTNET_CLI_TELEMETRY_OPTOUT** - Set to `1` to disable .NET telemetry
- **ERRORLOGVIEWER_DEFAULT_DIR** - Default log directory for ErrorLogViewer

### Application Settings

Configure via `appsettings.json`:

```json
{
  "ErrorLogViewer": {
    "DefaultLogDirectory": "C:\\Logs",
    "AutoStart": true,
    "MonitoringInterval": 1000,
    "MaxLogEntries": 10000
  }
}
```

---

## 🐛 Troubleshooting

### Common Issues

**Issue**: "Application already running" message
**Solution**: Close existing instance or use `--help` to see force options

**Issue**: Build failed
**Solution**: Ensure .NET 9.0 SDK is installed, run `dotnet --version` to verify

**Issue**: Missing dependencies
**Solution**: Run without `--skip-build` flag to rebuild

**Issue**: ErrorLogViewer crashes
**Solution**: Run `Test.ErrorLogViewer.bat` to diagnose, check logs in `logs/` directory

**Issue**: PowerShell execution policy error
**Solution**: Run `Set-ExecutionPolicy Bypass -Scope Process` before launching

---

## 📊 Logging

### Launcher Logs

PowerShell launcher creates detailed logs:
```
launcher-logs/
└── launcher-YYYYMMDD.log
```

### Application Logs

ErrorLogViewer logs:
```
tools/GGs.ErrorLogViewer/logs/
├── errorlogviewer-YYYYMMDD.log
└── early-logs.json
```

---

## 🔒 Security Considerations

1. **Single Instance Protection**: Prevents multiple instances from conflicting
2. **Safe Shutdown**: Graceful termination with resource cleanup
3. **Error Isolation**: Exceptions caught and logged, no silent failures
4. **Input Validation**: All command-line arguments validated
5. **Secure Defaults**: Release mode by default, debug only when specified

---

## 🏆 Best Practices

1. **Always test after updates**: Run `Test.ErrorLogViewer.bat` after code changes
2. **Use Release mode in production**: Only use `--debug` for troubleshooting
3. **Monitor logs regularly**: Check launcher-logs/ for issues
4. **Graceful shutdowns**: Close applications normally, avoid force-kill
5. **Keep logs organized**: Use custom log directories for different projects

---

## 📞 Support

For issues or questions:
1. Check this README
2. Review logs in `launcher-logs/` and application `logs/`
3. Run test suite: `Test.ErrorLogViewer.bat`
4. Check build output with `dotnet build --verbosity detailed`

---

## 📄 License

Part of the GGs Enterprise Suite - Internal Use

---

**Version**: 5.0.0  
**Last Updated**: 2025-10-02  
**Status**: ✅ Production Ready - All features complete, 0 placeholders, 0 errors
