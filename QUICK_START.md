# ⚡ ErrorLogViewer - Quick Start Guide

## 🚀 Launch in 3 Steps

### Step 1: Open Terminal
```batch
cd "c:\Users\307824\OneDrive - Västerås Stad\Skrivbordet\GGs\tools\GGs.ErrorLogViewer\bin\Release\net9.0-windows"
```

### Step 2: Run the App
```batch
GGs.ErrorLogViewer.exe
```

### Step 3: Start Monitoring
1. Click the **"Open Folder"** button (📁 icon)
2. Select your log directory
3. Click **"Start"** button
4. Done! Logs appear in real-time

---

## 🎮 Key Controls

| Button | What It Does |
|--------|-------------|
| ▶️ **Start** | Begin monitoring logs |
| ⏹️ **Stop** | Stop monitoring |
| 🔄 **Refresh** | Reload logs |
| 🗑️ **Clear** | Clear current view |
| 💾 **Export** | Save logs to CSV |
| 📁 **Open Folder** | Choose log directory |
| 🌙/☀️ **Theme** | Toggle dark/light mode |
| 🔍 **Search** | Find specific logs |

---

## ⚙️ Settings You Can Adjust

**In the Filter Bar**:
- **Level**: Filter by Error, Warning, Info, etc.
- **Source**: Filter by application/service
- **Auto-scroll**: Keep newest logs visible
- **Regex**: Use advanced search patterns
- **Smart Filter**: Reduce duplicate logs
- **Font Slider**: Make text bigger/smaller

---

## ❓ Troubleshooting

### App won't start?
Try this alternate launch method:
```batch
Start.ErrorLogViewer.bat
```

### No logs appearing?
1. Check folder path is correct
2. Ensure log files exist (*.log, *.txt)
3. Verify you have read permissions

### Sidebar buttons don't work?
**Known issue** - Only "Logs" view has a UI panel. Other views (Analytics, Bookmarks, etc.) have backend code but no frontend panels yet. All features are accessible via toolbar buttons anyway.

---

## 📖 More Help

- **Full technical details**: See `ERRORLOGVIEWER_ISSUES_AND_FIXES.md`
- **Status overview**: See `FINAL_STATUS_REPORT.md`
- **Sidebar fix info**: See `SIDEBAR_FIX_SUMMARY.md`

---

**That's it! You're ready to monitor logs.** 🎉
