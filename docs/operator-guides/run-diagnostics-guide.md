# Run Diagnostics - Step-by-Step Guide
**For Non-Technical Operators**  
**No Coding Knowledge Required**

---

## 🎯 What is the Diagnostics Tool?

The GGs Diagnostics Tool automatically checks your system for common problems and tells you exactly what's wrong. Think of it like a health check for your GGs Desktop application.

**You should run diagnostics when:**
- ✅ GGs Desktop won't start
- ✅ You see a "Recovery Mode" message
- ✅ The window is invisible or blank
- ✅ Support asks you to run diagnostics
- ✅ You want to check if everything is working correctly

---

## 🚀 Method 1: From Recovery Mode Window (EASIEST)

**Use this method if you see the Recovery Mode window**

### Step 1: Look for the Button
![Recovery Mode Window]
- You'll see a window titled "GGs Desktop - Recovery Mode"
- Look for a big blue button that says **"Run Diagnostics"**

### Step 2: Click the Button
- Click the **"Run Diagnostics"** button
- A progress bar will appear
- Wait 30-60 seconds (don't close the window!)

### Step 3: Review Results
- Results will appear in the window
- Look for colored indicators:
  - 🟢 **GREEN** = Everything is OK
  - 🟡 **YELLOW** = Warning (not critical)
  - 🔴 **RED** = Problem found (needs fixing)

### Step 4: Save the Report
- Click the **"Export Report"** button
- Report will be saved to your Desktop
- File name: `GGs-Diagnostics-Report-[date].txt`
- Send this file to support if needed

---

## 🖥️ Method 2: From Command Line (IF WINDOW WON'T OPEN)

**Use this method if GGs Desktop won't start at all**

### Step 1: Open Command Prompt
1. Press `Windows Key + R` on your keyboard
2. Type: `cmd`
3. Press Enter
4. A black window will appear (this is normal!)

### Step 2: Navigate to GGs Folder
1. In the black window, type:
   ```
   cd "C:\Users\[YourUsername]\Desktop\GGs"
   ```
   **Replace `[YourUsername]` with your actual Windows username**
   
2. Press Enter

**Example:**
```
cd "C:\Users\John\Desktop\GGs"
```

### Step 3: Run Diagnostics Command
1. Type exactly:
   ```
   GGs.Desktop.exe --diagnostics
   ```
2. Press Enter
3. Wait for the scan to complete (30-60 seconds)

### Step 4: Find the Report
1. The report will be saved automatically
2. Press `Windows Key + R`
3. Type: `%LOCALAPPDATA%\GGs\Diagnostics`
4. Press Enter
5. Look for the newest file (sorted by date)
6. Send this file to support if needed

---

## 📁 Method 3: From Launcher Batch File (RECOMMENDED FOR BEGINNERS)

**Use this method if you're not comfortable with command line**

### Step 1: Find the Launcher
1. Open File Explorer
2. Navigate to your GGs installation folder
3. Look for a file named: `Launch-Desktop.bat`
4. It has a gear icon ⚙️

### Step 2: Run the Launcher
1. Double-click `Launch-Desktop.bat`
2. A console window will appear with a menu
3. **DO NOT CLOSE THIS WINDOW**

### Step 3: Choose Diagnostics Option
1. Look at the menu options
2. Press the `D` key on your keyboard (for Diagnostics)
3. Press Enter
4. The diagnostics scan will start automatically

### Step 4: Follow On-Screen Instructions
1. The console will show progress messages
2. Wait for "Diagnostics Complete" message
3. Results will be displayed in the console
4. Press any key to save the report to your Desktop

---

## 📊 Understanding Diagnostics Results

### What Gets Checked

The diagnostics tool checks 15 different things:

| Check | What It Means |
|-------|---------------|
| **Files Present** | All required program files exist |
| **Configuration Valid** | Settings files are not corrupted |
| **Theme Resources** | Visual styles can be loaded |
| **Database Connection** | Can connect to local database |
| **Network Connectivity** | Can reach GGs online services |
| **Log Permissions** | Can write to log files |
| **Windows Version** | Your Windows is compatible |
| **.NET Runtime** | .NET 9 is installed correctly |
| **Memory Available** | Enough RAM to run GGs |
| **Disk Space** | Enough storage space |
| **Display Settings** | Monitor configuration is valid |
| **User Permissions** | You have required access rights |
| **Port Availability** | Required network ports are open |
| **Antivirus Conflicts** | No security software blocking GGs |
| **Previous Crashes** | Checks for crash dump files |

### Reading the Results

#### 🟢 GREEN - All Clear
```
✓ Files Present: OK
✓ Configuration Valid: OK
✓ Theme Resources: OK
```
**What to do:** Nothing! Everything is working correctly.

#### 🟡 YELLOW - Warning
```
⚠ Network Connectivity: Slow response (2500ms)
⚠ Disk Space: Only 5GB remaining
```
**What to do:** These won't stop GGs from working, but you should address them soon.

#### 🔴 RED - Error
```
✗ Theme Resources: EnterpriseControlStyles.xaml missing
✗ .NET Runtime: .NET 9 not found
```
**What to do:** These MUST be fixed. Follow the recommended actions in the report.

---

## 🔧 Common Problems and Fixes

### Problem: "Theme Resources: FAILED"
**What it means:** Visual style files are missing or corrupted

**How to fix:**
1. Close GGs Desktop
2. Navigate to: `GGs\clients\GGs.Desktop\Themes\`
3. Check if these files exist:
   - `EnterpriseControlStyles.xaml`
   - `ModernTheme.xaml`
4. If missing, reinstall GGs Desktop
5. Run diagnostics again

---

### Problem: ".NET Runtime: NOT FOUND"
**What it means:** .NET 9 is not installed on your computer

**How to fix:**
1. Go to: https://dotnet.microsoft.com/download/dotnet/9.0
2. Download ".NET Desktop Runtime 9.0"
3. Run the installer
4. Restart your computer
5. Run diagnostics again

---

### Problem: "Configuration Valid: FAILED"
**What it means:** Settings file is corrupted

**How to fix:**
1. Press `Windows Key + R`
2. Type: `%LOCALAPPDATA%\GGs\Config`
3. Press Enter
4. Rename `appsettings.json` to `appsettings.json.backup`
5. Launch GGs Desktop (new config will be created)
6. Run diagnostics again

---

### Problem: "Database Connection: FAILED"
**What it means:** Can't connect to local database

**How to fix:**
1. Close GGs Desktop
2. Press `Windows Key + R`
3. Type: `%LOCALAPPDATA%\GGs\Database`
4. Press Enter
5. Delete `ggs.db` (it will be recreated)
6. Launch GGs Desktop
7. Run diagnostics again

---

### Problem: "Log Permissions: FAILED"
**What it means:** Can't write to log files (usually antivirus blocking)

**How to fix:**
1. Open your antivirus software
2. Add GGs folder to exclusions/whitelist
3. Restart your computer
4. Run diagnostics again

---

## 📤 Sending Diagnostics to Support

### What to Include

When contacting support, send:
1. ✅ **Diagnostics report** (the .txt file)
2. ✅ **Screenshot** of the error or problem
3. ✅ **Desktop log file** from `%LOCALAPPDATA%\GGs\Logs\desktop.log`

### How to Package Files

#### Option 1: Email
1. Create a new email to support
2. Attach the diagnostics report
3. Attach the desktop.log file
4. Paste or attach screenshot
5. Describe what you were doing when the problem occurred

#### Option 2: Zip File
1. Create a new folder on your Desktop
2. Name it: `GGs-Support-[YourName]-[Date]`
3. Copy these files into the folder:
   - Diagnostics report
   - desktop.log
   - Screenshot
4. Right-click the folder → Send to → Compressed (zipped) folder
5. Send the .zip file to support

---

## ⏱️ How Long Does Diagnostics Take?

| Method | Time Required |
|--------|---------------|
| From Recovery Mode | 30-45 seconds |
| From Command Line | 45-60 seconds |
| From Launcher | 60-90 seconds |

**Note:** First run may take longer as it initializes the diagnostics system.

---

## 🔄 Running Diagnostics Regularly

### Recommended Schedule

- ✅ **After installation** - Verify everything is set up correctly
- ✅ **After Windows updates** - Check for compatibility issues
- ✅ **Monthly** - Preventive health check
- ✅ **Before contacting support** - Gather diagnostic information
- ✅ **After any problems** - Verify the fix worked

### Automated Diagnostics

You can set up automatic diagnostics:
1. Open `Launch-Desktop.bat`
2. Press `S` for Settings
3. Enable "Run diagnostics on startup"
4. Diagnostics will run automatically each time you launch GGs

---

## ❓ Frequently Asked Questions

### Q: Do I need admin rights to run diagnostics?
**A:** No! Diagnostics work perfectly in non-admin mode.

### Q: Will diagnostics fix problems automatically?
**A:** No, diagnostics only identifies problems. You'll need to follow the recommended fixes.

### Q: Can I run diagnostics while GGs Desktop is running?
**A:** Yes, but it's better to close GGs Desktop first for accurate results.

### Q: How much disk space do diagnostic reports use?
**A:** Each report is about 50-100 KB. Old reports are automatically deleted after 30 days.

### Q: What if diagnostics itself crashes?
**A:** This is rare. If it happens:
1. Check `%LOCALAPPDATA%\GGs\Logs\diagnostics.log`
2. Send this file to support
3. They can run remote diagnostics

### Q: Can I delete old diagnostic reports?
**A:** Yes! Navigate to `%LOCALAPPDATA%\GGs\Diagnostics\` and delete old files.

---

## 🎓 Video Tutorial

**Coming Soon:** We're creating a video walkthrough showing each method.

In the meantime, follow the step-by-step instructions above. They're designed for complete beginners!

---

## 📞 Still Need Help?

If diagnostics doesn't solve your problem:

1. **Check the main troubleshooting guide**: `docs/operator-guides/desktop-troubleshooting-guide.md`
2. **Contact support** with your diagnostics report
3. **Join the community forum** for peer help

**Support Email:** support@ggs.example.com  
**Support Hours:** Monday-Friday, 9 AM - 5 PM

---

## 🎯 Quick Reference Card

```
┌──────────────────────────────────────────────────┐
│         Run Diagnostics - Quick Guide            │
├──────────────────────────────────────────────────┤
│ Method 1: Recovery Mode Window                   │
│   → Click "Run Diagnostics" button               │
│   → Wait 30-60 seconds                           │
│   → Click "Export Report"                        │
├──────────────────────────────────────────────────┤
│ Method 2: Command Line                           │
│   → Open cmd (Win+R, type cmd)                   │
│   → cd "C:\Path\To\GGs"                          │
│   → GGs.Desktop.exe --diagnostics                │
├──────────────────────────────────────────────────┤
│ Method 3: Launcher (Easiest!)                    │
│   → Double-click Launch-Desktop.bat              │
│   → Press D for Diagnostics                      │
│   → Follow on-screen instructions                │
├──────────────────────────────────────────────────┤
│ Results Location:                                │
│   %LOCALAPPDATA%\GGs\Diagnostics\                │
│                                                  │
│ 🟢 GREEN = OK  🟡 YELLOW = Warning  🔴 RED = Error │
└──────────────────────────────────────────────────┘
```

---

**Document Version**: 1.0  
**Last Updated**: 2025-10-04  
**Maintained By**: GGs Engineering Team

