# 🚀 GGs Launcher Suite - User Guide

## Overview

The GGs Launcher Suite is an enterprise-grade application management system designed for **zero coding knowledge required**. Simply double-click any launcher to start your applications!

## 📁 Available Launchers

### 1. **Launch-All-New.bat** ⭐ (RECOMMENDED)
**The easiest way to start everything!**

- **What it does**: Launches all GGs components (Server, Desktop, Viewer) in one click
- **Features**:
  - ✅ Automatically builds all components
  - ✅ Checks for .NET installation
  - ✅ Manages port conflicts
  - ✅ Shows interactive dashboard with real-time status
  - ✅ Easy controls: [R] Refresh, [Q] Quit All, [L] View Logs
- **How to use**: 
  1. Double-click `Launch-All-New.bat`
  2. Wait for all components to start (takes ~15-30 seconds)
  3. Use the dashboard to monitor status
  4. Press [Q] when you want to close everything

### 2. **Launch-Server-New.bat** 🌐
**Starts the web server only**

- **What it does**: Launches the GGs web server on http://localhost:5000
- **Features**:
  - ✅ Checks port 5000 availability
  - ✅ Automatically frees the port if occupied
  - ✅ Builds the server project
  - ✅ Shows server uptime and status
- **How to use**:
  1. Double-click `Launch-Server-New.bat`
  2. Wait for "Server is listening on port 5000"
  3. Access the server at http://localhost:5000
  4. Close the window to stop monitoring (server continues running)

### 3. **Launch-Desktop-New.bat** 💻
**Starts the desktop application only**

- **What it does**: Launches the GGs desktop GUI application
- **Features**:
  - ✅ Checks for existing instances
  - ✅ Validates .NET runtime
  - ✅ Monitors application status
  - ✅ Tracks uptime
- **How to use**:
  1. Double-click `Launch-Desktop-New.bat`
  2. The desktop application window will appear
  3. Use the application normally
  4. Close the application window when done

### 4. **Launch-Viewer-New.bat** 🔍
**Starts the error log viewer only**

- **What it does**: Launches the GGs Error Log Viewer tool
- **Features**:
  - ✅ Validates application path
  - ✅ Cleans up old instances
  - ✅ Real-time monitoring
  - ✅ Shutdown detection
- **How to use**:
  1. Double-click `Launch-Viewer-New.bat`
  2. The viewer window will appear
  3. View and analyze error logs
  4. Close the viewer window when done

## 🎯 Quick Start Guide (For Non-Technical Users)

### First Time Setup

1. **Install .NET 9.0 SDK** (if not already installed)
   - The launcher will tell you if you need this
   - Download from: https://dotnet.microsoft.com/download
   - Run the installer
   - Restart your computer

2. **Run the Test Script** (Optional but recommended)
   - Double-click `Test-Launchers.bat`
   - Wait for all tests to complete
   - You should see "All tests passed!"

3. **Start Everything**
   - Double-click `Launch-All-New.bat`
   - Wait for the dashboard to appear
   - All components will start automatically!

### Daily Usage

**Option A: Start Everything (Recommended)**
- Double-click `Launch-All-New.bat`
- Wait for the dashboard
- All components are now running!

**Option B: Start Individual Components**
- Need only the server? → Double-click `Launch-Server-New.bat`
- Need only the desktop app? → Double-click `Launch-Desktop-New.bat`
- Need only the viewer? → Double-click `Launch-Viewer-New.bat`

### Stopping Applications

**If using Launch-All-New.bat:**
- Press `Q` in the dashboard window
- All components will close automatically

**If using individual launchers:**
- Close the application window
- Or close the launcher window (app continues running)

## 📊 Understanding the Dashboard (Launch-All-New.bat)

When you run `Launch-All-New.bat`, you'll see a dashboard like this:

```
============================================================================
 GGs Launcher Suite - Interactive Dashboard
============================================================================

 .NET Version: 9.0.305
 Server URL: http://localhost:5000
 Log Directory: launcher-logs

============================================================================
 COMPONENT STATUS
============================================================================

 [SERVER]
   Status: RUNNING | Uptime: 45s | Port: 5000
   URL: http://localhost:5000

 [DESKTOP]
   Status: RUNNING | Uptime: 40s
   Type: GUI Application

 [VIEWER]
   Status: RUNNING | Uptime: 35s
   Type: Error Log Viewer

============================================================================
 CONTROLS
============================================================================

 [R] Refresh Status
 [Q] Quit All Applications
 [L] View Logs Directory
 [S] View Server Logs
 [D] View Desktop Logs
 [V] View Viewer Logs
```

### Dashboard Controls

- **[R] Refresh**: Updates the status (auto-refreshes every 5 seconds)
- **[Q] Quit All**: Closes all GGs applications safely
- **[L] View Logs**: Opens the logs folder
- **[S] View Server Logs**: Opens server log file
- **[D] View Desktop Logs**: Opens desktop log file
- **[V] View Viewer Logs**: Opens viewer log file

## 📝 Logs and Troubleshooting

### Where are the logs?

All logs are saved in the `launcher-logs` folder:
- `master-launcher.log` - Main orchestrator log
- `server-launcher.log` - Server launcher log
- `desktop-launcher.log` - Desktop launcher log
- `viewer-launcher.log` - Viewer launcher log
- `test-results.log` - Test script results

### Common Issues and Solutions

#### ❌ "Application executable not found"

**Problem**: The application hasn't been built yet.

**Solution**:
1. Use `Launch-All-New.bat` instead (it builds automatically)
2. Or run: `dotnet build` in the project folder

#### ❌ ".NET SDK not found"

**Problem**: .NET is not installed or not in PATH.

**Solution**:
1. Download .NET 9.0 SDK from https://dotnet.microsoft.com/download
2. Install it
3. Restart your computer
4. Try again

#### ❌ "Port 5000 is already in use"

**Problem**: Another application is using port 5000.

**Solution**:
- The launcher will automatically free the port
- If it fails, close other applications using port 5000
- Or restart your computer

#### ❌ "Application failed to start"

**Problem**: The application crashed immediately.

**Solution**:
1. Check the log files in `launcher-logs` folder
2. Look for error messages
3. Try running `Test-Launchers.bat` to diagnose issues
4. Ensure all dependencies are installed

#### ❌ "Build failed"

**Problem**: Compilation errors in the code.

**Solution**:
1. Open the log file to see the error
2. Run `dotnet restore` to restore packages
3. Check if the code has syntax errors
4. Contact the development team

## 🎨 Color Coding

Each launcher has a unique color for easy identification:
- 🟢 **Launch-Viewer-New.bat**: Green
- 🔵 **Launch-Desktop-New.bat**: Blue
- 🟡 **Launch-Server-New.bat**: Yellow
- ⚪ **Launch-All-New.bat**: White
- 🟣 **Test-Launchers.bat**: Purple

## 🔧 Advanced Features

### For Power Users

#### Environment Variables
You can customize the launchers by editing these variables at the top of each file:
- `SERVER_PORT` - Change the server port (default: 5000)
- `SERVER_URL` - Change the server URL
- `LOG_DIR` - Change the log directory location

#### Command Line Usage
You can also run the launchers from Command Prompt:
```batch
cd "path\to\GGs"
Launch-All-New.bat
```

#### Automation
The launchers support automation with proper exit codes:
- Exit code 0 = Success
- Exit code 1 = Failure

## 📋 Feature Checklist

### ✅ All Launchers Include:
- [x] Process management (kill existing instances)
- [x] Comprehensive logging with timestamps
- [x] Error handling with troubleshooting hints
- [x] Cross-platform date/time formatting
- [x] Application validation before launch
- [x] Graceful cleanup and proper exit codes
- [x] Real-time monitoring
- [x] User-friendly messages

### ✅ Launch-All-New.bat Extras:
- [x] Interactive dashboard
- [x] Multi-component coordination
- [x] Staggered launch timing
- [x] Graceful shutdown of all components
- [x] Error recovery (continues if one fails)
- [x] Quick access to all logs
- [x] Real-time status updates

### ✅ Launch-Server-New.bat Extras:
- [x] Port availability checking
- [x] Automatic port freeing
- [x] Smart build (server only)
- [x] .NET version validation
- [x] Server health monitoring

## 🆘 Getting Help

### If something doesn't work:

1. **Run the test script**: `Test-Launchers.bat`
2. **Check the logs**: Open `launcher-logs` folder
3. **Read error messages**: The launchers provide detailed troubleshooting hints
4. **Try Launch-All-New.bat**: It has the most comprehensive error handling

### Contact Information

For technical support, contact the GGs development team with:
- The error message you received
- The log files from `launcher-logs` folder
- What you were trying to do
- Your .NET version (shown in the dashboard)

## 🎓 Tips for Best Experience

1. **Always use Launch-All-New.bat** for the easiest experience
2. **Don't close the dashboard window** if you want to monitor status
3. **Check logs regularly** to catch issues early
4. **Run Test-Launchers.bat** after updates to ensure everything works
5. **Keep .NET updated** to the latest version
6. **Close applications properly** using [Q] in the dashboard

## 📦 What Gets Installed/Created

The launchers create these folders and files:
- `launcher-logs/` - Log files directory
- `launcher-logs/*.log` - Individual log files
- No other files are created or modified

**Safe to delete**: You can safely delete the `launcher-logs` folder anytime. It will be recreated automatically.

## 🚀 Performance Tips

- **First launch is slower** (needs to build everything)
- **Subsequent launches are faster** (uses cached builds)
- **Close unused components** to free up resources
- **Use individual launchers** if you only need one component

## ✨ Summary

The GGs Launcher Suite makes it **incredibly easy** to manage complex applications:

- ✅ **No coding knowledge required**
- ✅ **Just double-click and go**
- ✅ **Automatic error handling**
- ✅ **Clear status messages**
- ✅ **Comprehensive logging**
- ✅ **Easy troubleshooting**

**Recommended workflow**: Double-click `Launch-All-New.bat` → Wait for dashboard → Start working!

---

*Last Updated: 2025-10-02*
*Version: 1.0.0*
*Enterprise-Grade Application Management*
