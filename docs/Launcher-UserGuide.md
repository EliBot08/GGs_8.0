# GGs LaunchControl User Guide

**For Non-Technical Users - Zero Coding Knowledge Required**

---

## Quick Start (3 Simple Steps)

### Step 1: Choose What You Want to Launch

You have **3 options** - just double-click the file you want:

1. **`launch-desktop.cmd`** - Launch the main GGs Desktop application
2. **`launch-errorlogviewer.cmd`** - Launch the diagnostic log viewer
3. **`launch-fusion.cmd`** - Launch both Desktop and ErrorLogViewer together

### Step 2: Watch the Neon Screen

You'll see a colorful screen with the GGs logo. This is normal! The launcher is:
- ✓ Checking your system is ready
- ✓ Making sure all files are in place
- ✓ Starting the application(s)

### Step 3: Done!

The application will open automatically. You can close the launcher window.

---

## What Each Launcher Does

### 🖥️ Desktop Launcher (`launch-desktop.cmd`)
**What it does:** Starts the main GGs Desktop application with the full user interface.

**When to use it:** This is your main application for daily use.

**What you'll see:**
```
███████╗ ██████╗  ██████╗ ███████╗
██╔════╝██╔════╝ ██╔════╝ ██╔════╝
██║  ███╗██║  ███╗███████╗ ███████╗
██║   ██║██║   ██║╚════██║ ╚════██║
╚██████╔╝╚██████╔╝██████╔╝ ███████║
 ╚═════╝  ╚═════╝ ╚═════╝  ╚══════╝

Desktop Application Launcher
```

### 📋 ErrorLogViewer Launcher (`launch-errorlogviewer.cmd`)
**What it does:** Opens the diagnostic tool to view system logs and errors.

**When to use it:** When you need to troubleshoot issues or view detailed logs.

**What you'll see:** Same neon logo but in yellow color.

### 🔀 Fusion Launcher (`launch-fusion.cmd`)
**What it does:** Starts both Desktop and ErrorLogViewer at the same time.

**When to use it:** When you want both applications running together for advanced monitoring.

**What you'll see:** Same neon logo but in green color.

---

## Understanding the Status Messages

### ✓ Green Messages = Good
- `[SUCCESS]` - Everything worked perfectly
- `✓ PASS` - Health check passed
- `✓ RUNNING` - Application is running

### ⚠ Yellow Messages = Information (Not Errors!)
- `Admin privileges were requested but DECLINED BY OPERATOR` - **This is normal!** The app continues without admin rights.
- `Continuing in non-elevated mode (this is normal and expected)` - **This is expected behavior!**

### ✗ Red Messages = Problems
- `[ERROR]` - Something went wrong
- `✗ FAIL` - A required check failed
- `✗ FAILED` - Application didn't start

---

## Admin Privileges - What You Need to Know

### The Important Part: **You Don't Need Admin Rights!**

When you see this message:
```
⚠ Admin privileges were requested but DECLINED BY OPERATOR
Continuing in non-elevated mode (this is normal and expected)
```

**This is GOOD!** It means:
- ✓ You clicked "No" on the UAC prompt (or it was automatically declined)
- ✓ The application is running safely without admin rights
- ✓ Everything will work normally

### When Admin Rights Are Actually Needed

Some advanced features require admin rights. If you need them:
1. Right-click the launcher file
2. Choose "Run as administrator"
3. Click "Yes" on the UAC prompt

**But for 99% of daily use, you don't need this!**

---

## Troubleshooting

### Problem: "LaunchControl not found"

**What it means:** The launcher program hasn't been built yet.

**How to fix:**
1. Open a command prompt in the GGs folder
2. Type: `dotnet build GGs\GGs.sln -c Release`
3. Wait for it to finish
4. Try the launcher again

### Problem: "Executable not found"

**What it means:** The application files are missing.

**How to fix:**
1. Make sure you built the solution (see above)
2. Check that the `bin` folder exists in the GGs directory
3. If still not working, rebuild: `dotnet build GGs\GGs.sln -c Release`

### Problem: Application starts but closes immediately

**What to do:**
1. Check the logs in the `launcher-logs` folder
2. Look for the most recent file (sorted by date)
3. Open it with Notepad to see what went wrong
4. Share the log file with support if you need help

### Problem: "Port already in use"

**What it means:** Another copy of the application is already running.

**How to fix:**
1. Close any existing GGs applications
2. Open Task Manager (Ctrl+Shift+Esc)
3. Look for `GGs.Desktop.exe` or `GGs.ErrorLogViewer.exe`
4. End those tasks
5. Try the launcher again

---

## Advanced Options (Optional)

### Running in Diagnostic Mode

If support asks you to run in diagnostic mode:
1. Open a command prompt
2. Navigate to the GGs folder
3. Type: `tools\GGs.LaunchControl\bin\Release\net9.0-windows\win-x64\publish\GGs.LaunchControl.exe --profile desktop --mode diag`

### Running in Test Mode

For testing without affecting your system:
1. Open a command prompt
2. Navigate to the GGs folder
3. Type: `tools\GGs.LaunchControl\bin\Release\net9.0-windows\win-x64\publish\GGs.LaunchControl.exe --profile desktop --mode test`

### Getting Help

Type this to see all options:
```
tools\GGs.LaunchControl\bin\Release\net9.0-windows\win-x64\publish\GGs.LaunchControl.exe --help
```

---

## Where Are the Logs?

All launcher activity is logged to: `launcher-logs\`

Log files are named like: `desktop-20251004-143022-normal.log`
- `desktop` = which profile was used
- `20251004-143022` = date and time (YYYYMMDD-HHMMSS)
- `normal` = which mode was used

You can open these with Notepad to see exactly what happened.

---

## Health Checks Explained

Before launching, the system checks:

| Check | What It Means |
|-------|---------------|
| ✓ .NET 9.0 Runtime | The required software framework is installed |
| ✓ Desktop binaries directory | The application files folder exists |
| ✓ Desktop executable | The main program file exists |
| ✓ Minimum disk space | You have enough free disk space |

If any check fails, the launcher will tell you exactly what's wrong and how to fix it.

---

## Summary

**Remember:**
1. Just double-click the launcher you want
2. Yellow "admin declined" messages are NORMAL and EXPECTED
3. Green messages mean success
4. Red messages mean problems (check the logs)
5. You don't need coding knowledge - the launchers do everything for you!

**Need Help?**
- Check the logs in `launcher-logs\`
- Read the error message carefully - it usually tells you what to do
- Contact support with your log file if you're stuck

---

**Version:** 1.0.0  
**Last Updated:** 2025-10-04

