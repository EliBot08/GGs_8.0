# GGs Desktop Troubleshooting Guide for Operators
**Version**: 1.0  
**Last Updated**: 2025-10-04  
**Audience**: Non-technical operators with zero coding knowledge

---

## 🚀 Quick Start - Is Everything Working?

### ✅ Normal Launch Checklist
When you launch GGs Desktop, you should see:
1. ✅ **Main window opens** within 5-10 seconds
2. ✅ **Dashboard displays** with optimization, network, and system intelligence tabs
3. ✅ **No "Recovery Mode" banner** at the top
4. ✅ **No admin login dialogs** appear

If you see all of the above, **everything is working correctly!** You can skip this guide.

---

## ⚠️ When to Use This Guide

Use this guide if you experience any of these issues:

| Problem | What You See | What It Means |
|---------|--------------|---------------|
| **Recovery Mode** | Window shows "GGs Desktop - Recovery Mode" in title bar | The main dashboard failed to load |
| **Admin Login Dialog** | Pop-up asking for admin username/password | Legacy feature incorrectly triggered |
| **Invisible Window** | Launcher says "success" but no window appears | Window is off-screen or hidden |
| **Crash on Startup** | Window opens then immediately closes | Critical error during initialization |

---

## 🔍 Troubleshooting Flowchart

```
START: Launch GGs Desktop
    ↓
Does window open?
    ├─ NO → See "Window Won't Open" section
    └─ YES → Continue
         ↓
    Does title bar say "Recovery Mode"?
         ├─ YES → See "Recovery Mode" section
         └─ NO → Continue
              ↓
         Do you see admin login dialog?
              ├─ YES → See "Admin Login Dialog" section
              └─ NO → Continue
                   ↓
              Can you see dashboard tabs?
                   ├─ NO → See "Blank Window" section
                   └─ YES → ✅ WORKING CORRECTLY
```

---

## 📋 Problem 1: Window Won't Open

### Symptoms
- You double-click the launcher
- Console window appears briefly
- No GGs Desktop window appears

### Step-by-Step Fix

#### Step 1: Check if Process is Running
1. Press `Ctrl + Shift + Esc` to open Task Manager
2. Look for "GGs.Desktop" in the list
3. If you see it:
   - Right-click → "End Task"
   - Wait 5 seconds
   - Try launching again

#### Step 2: Check Log Files
1. Press `Windows Key + R`
2. Type: `%LOCALAPPDATA%\GGs\Logs`
3. Press Enter
4. Open the newest `desktop.log` file
5. Look for lines containing "ERROR" or "FATAL"
6. Take a screenshot and send to support

#### Step 3: Run Diagnostics
1. Navigate to your GGs installation folder
2. Double-click `Launch-Desktop.bat`
3. Watch the console output
4. Look for any red error messages
5. Take a screenshot of the console

#### Step 4: Clean Restart
1. Close all GGs applications
2. Press `Windows Key + R`
3. Type: `%LOCALAPPDATA%\GGs`
4. Press Enter
5. Rename the `GGs` folder to `GGs.backup`
6. Try launching again (fresh configuration will be created)

---

## 📋 Problem 2: Recovery Mode Banner

### Symptoms
- Window opens successfully
- Title bar shows "GGs Desktop - Recovery Mode"
- Dashboard is replaced with error message

### What This Means
The main dashboard failed to load, so GGs switched to a simplified recovery interface to help you diagnose the problem.

### Step-by-Step Fix

#### Step 1: Read the Recovery Message
1. Look at the recovery window
2. It will show specific error details
3. Common messages:
   - **"Resource dictionary failed to load"** → Theme files are corrupted
   - **"View initialization failed"** → Dashboard components are missing
   - **"Dependency injection error"** → Configuration issue

#### Step 2: Check Theme Files
1. Navigate to: `GGs\clients\GGs.Desktop\Themes\`
2. Verify these files exist:
   - `EnterpriseControlStyles.xaml`
   - `ModernTheme.xaml`
3. If any are missing, reinstall GGs Desktop

#### Step 3: Clear Cache and Restart
1. Close GGs Desktop
2. Press `Windows Key + R`
3. Type: `%LOCALAPPDATA%\GGs\Cache`
4. Press Enter
5. Delete all files in this folder
6. Launch GGs Desktop again

#### Step 4: Check for Updates
1. Navigate to your GGs installation folder
2. Look for `CHANGELOG.md`
3. Check if your version is outdated
4. Download latest version if needed

---

## 📋 Problem 3: Admin Login Dialog

### Symptoms
- Pop-up window appears asking for admin credentials
- Title: "GGs - Admin Login"
- You don't have admin credentials

### What This Means
A legacy authentication feature was incorrectly triggered. **You should never see this dialog.**

### Step-by-Step Fix

#### Step 1: Close the Dialog
1. Click the "Cancel" or "X" button
2. **DO NOT** enter any credentials
3. The application should continue without admin access

#### Step 2: Check Feature Flags
1. Navigate to: `%LOCALAPPDATA%\GGs\Config\`
2. Open `appsettings.json` in Notepad
3. Look for: `"RequireAdminLogin": true`
4. Change to: `"RequireAdminLogin": false`
5. Save the file
6. Restart GGs Desktop

#### Step 3: Report the Issue
This dialog should not appear. Please:
1. Take a screenshot of the dialog
2. Note what you were doing when it appeared
3. Send to support with log files

---

## 📋 Problem 4: Blank or Invisible Window

### Symptoms
- Launcher says "GGs Desktop started successfully"
- Task Manager shows GGs.Desktop process running
- No visible window on screen

### Step-by-Step Fix

#### Step 1: Check if Window is Off-Screen
1. Press `Alt + Tab` to cycle through windows
2. Select "GGs Desktop" if you see it
3. Press `Alt + Space` to open window menu
4. Press `M` for Move
5. Use arrow keys to move window back on screen

#### Step 2: Reset Window Position
1. Close GGs Desktop
2. Press `Windows Key + R`
3. Type: `%LOCALAPPDATA%\GGs\Config\window-state.json`
4. Delete this file
5. Launch GGs Desktop again (window will appear centered)

#### Step 3: Check Display Settings
1. Right-click desktop → Display Settings
2. Verify you're not in "Extend" mode with disconnected monitor
3. If you recently unplugged a second monitor:
   - Press `Windows Key + P`
   - Select "PC screen only"
   - Try launching again

---

## 🔧 Run Diagnostics Button Sequence

### Automated Diagnostics Tool

GGs Desktop includes a built-in diagnostics tool. Here's how to use it:

#### Method 1: From Recovery Mode
1. If you see the Recovery Mode window
2. Click the **"Run Diagnostics"** button
3. Wait for the scan to complete (30-60 seconds)
4. Review the results in the diagnostics report
5. Click **"Export Report"** to save to desktop

#### Method 2: From Command Line
1. Open Command Prompt (no admin needed)
2. Navigate to GGs installation folder:
   ```
   cd "C:\Path\To\GGs"
   ```
3. Run diagnostics:
   ```
   GGs.Desktop.exe --diagnostics
   ```
4. Report will be saved to: `%LOCALAPPDATA%\GGs\Diagnostics\`

#### Method 3: From Launcher
1. Navigate to GGs installation folder
2. Double-click `Launch-Desktop.bat`
3. When console appears, press `D` for Diagnostics
4. Follow on-screen prompts

### What Diagnostics Checks

The diagnostics tool automatically checks:
- ✅ All required files are present
- ✅ Configuration files are valid JSON
- ✅ Theme resources can be loaded
- ✅ Database connections work
- ✅ Network connectivity to GGs services
- ✅ Log file permissions
- ✅ Windows version compatibility
- ✅ .NET 9 runtime is installed

### Reading Diagnostics Results

Results are color-coded:
- 🟢 **GREEN**: Everything is working correctly
- 🟡 **YELLOW**: Warning - may cause issues but not critical
- 🔴 **RED**: Error - must be fixed for proper operation

---

## 📞 When to Contact Support

Contact support if:
1. ❌ Diagnostics show RED errors you can't fix
2. ❌ Recovery Mode persists after following all steps
3. ❌ Admin Login dialog keeps appearing
4. ❌ Application crashes repeatedly
5. ❌ You see error messages not covered in this guide

### Information to Provide
When contacting support, please provide:
1. **Screenshot** of the error or problem
2. **Log files** from `%LOCALAPPDATA%\GGs\Logs\`
3. **Diagnostics report** (if you ran diagnostics)
4. **Steps to reproduce** the problem
5. **Windows version** (press `Windows Key + R`, type `winver`, press Enter)

---

## 📚 Additional Resources

### Log File Locations
- **Desktop Logs**: `%LOCALAPPDATA%\GGs\Logs\desktop.log`
- **Launcher Logs**: `GGs\launcher-logs\`
- **Diagnostics Reports**: `%LOCALAPPDATA%\GGs\Diagnostics\`

### Configuration Files
- **App Settings**: `%LOCALAPPDATA%\GGs\Config\appsettings.json`
- **Window State**: `%LOCALAPPDATA%\GGs\Config\window-state.json`
- **User Preferences**: `%LOCALAPPDATA%\GGs\Config\user-preferences.json`

### Keyboard Shortcuts
- `Ctrl + D` - Open diagnostics panel
- `Ctrl + L` - Open log viewer
- `Ctrl + R` - Reload dashboard
- `F5` - Refresh current view
- `F12` - Open developer tools (for advanced users)

---

## 🎯 Quick Reference Card

Print this section and keep it near your computer:

```
┌─────────────────────────────────────────────────────┐
│         GGs Desktop Quick Troubleshooting           │
├─────────────────────────────────────────────────────┤
│ Problem              │ Quick Fix                    │
├──────────────────────┼──────────────────────────────┤
│ Won't start          │ Check Task Manager, kill     │
│                      │ existing process, try again  │
├──────────────────────┼──────────────────────────────┤
│ Recovery Mode        │ Clear cache folder, restart  │
│                      │ %LOCALAPPDATA%\GGs\Cache     │
├──────────────────────┼──────────────────────────────┤
│ Admin Login Dialog   │ Click Cancel, check          │
│                      │ appsettings.json             │
├──────────────────────┼──────────────────────────────┤
│ Invisible Window     │ Alt+Tab, Alt+Space, M,       │
│                      │ arrow keys to move           │
├──────────────────────┼──────────────────────────────┤
│ Any persistent issue │ Run: GGs.Desktop.exe         │
│                      │      --diagnostics           │
└──────────────────────┴──────────────────────────────┘

Log Files: %LOCALAPPDATA%\GGs\Logs\desktop.log
Support: Include logs + screenshots when reporting
```

---

**Document Version**: 1.0  
**Last Reviewed**: 2025-10-04  
**Maintained By**: GGs Engineering Team

