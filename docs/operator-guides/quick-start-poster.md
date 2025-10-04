# GGs Desktop Quick Start Poster
**Print this page and keep it near your computer!**

---

```
╔══════════════════════════════════════════════════════════════════════════╗
║                                                                          ║
║                    🚀 GGs DESKTOP QUICK START 🚀                         ║
║                                                                          ║
║                        For Non-Technical Users                           ║
║                                                                          ║
╚══════════════════════════════════════════════════════════════════════════╝


┌──────────────────────────────────────────────────────────────────────────┐
│  ✅ NORMAL LAUNCH - What You Should See                                  │
├──────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  1. Double-click launcher                                                │
│  2. Window opens in 5-10 seconds                                         │
│  3. Dashboard displays with tabs                                         │
│  4. NO "Recovery Mode" in title                                          │
│  5. NO admin login dialogs                                               │
│                                                                          │
│  If you see all of the above → Everything is working! ✓                 │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘


┌──────────────────────────────────────────────────────────────────────────┐
│  ⚠️  PROBLEM QUICK FIXES                                                 │
├──────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  Problem: Window Won't Open                                              │
│  ├─ Open Task Manager (Ctrl+Shift+Esc)                                  │
│  ├─ Find "GGs.Desktop" → Right-click → End Task                         │
│  ├─ Wait 5 seconds                                                       │
│  └─ Try launching again                                                  │
│                                                                          │
│  ─────────────────────────────────────────────────────────────────────  │
│                                                                          │
│  Problem: "Recovery Mode" Banner                                         │
│  ├─ Close GGs Desktop                                                    │
│  ├─ Press Win+R, type: %LOCALAPPDATA%\GGs\Cache                         │
│  ├─ Delete all files in Cache folder                                    │
│  └─ Launch GGs Desktop again                                             │
│                                                                          │
│  ─────────────────────────────────────────────────────────────────────  │
│                                                                          │
│  Problem: Admin Login Dialog Appears                                     │
│  ├─ Click "Cancel" (DO NOT enter credentials!)                          │
│  ├─ Application will continue without admin                             │
│  └─ Report this to support (should not happen)                          │
│                                                                          │
│  ─────────────────────────────────────────────────────────────────────  │
│                                                                          │
│  Problem: Window is Invisible                                            │
│  ├─ Press Alt+Tab to select GGs Desktop                                 │
│  ├─ Press Alt+Space, then M                                             │
│  ├─ Use arrow keys to move window                                       │
│  └─ Window should appear on screen                                      │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘


┌──────────────────────────────────────────────────────────────────────────┐
│  🔧 RUN DIAGNOSTICS - 3 Easy Methods                                     │
├──────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  Method 1: From Recovery Mode (EASIEST)                                  │
│  └─ Click the "Run Diagnostics" button in Recovery window               │
│                                                                          │
│  Method 2: From Launcher (RECOMMENDED)                                   │
│  ├─ Double-click Launch-Desktop.bat                                     │
│  ├─ Press D when menu appears                                           │
│  └─ Follow on-screen instructions                                       │
│                                                                          │
│  Method 3: From Command Line                                             │
│  ├─ Press Win+R, type: cmd                                              │
│  ├─ Type: cd "C:\Path\To\GGs"                                           │
│  └─ Type: GGs.Desktop.exe --diagnostics                                 │
│                                                                          │
│  Results saved to: %LOCALAPPDATA%\GGs\Diagnostics\                      │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘


┌──────────────────────────────────────────────────────────────────────────┐
│  📊 UNDERSTANDING DIAGNOSTICS RESULTS                                    │
├──────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  🟢 GREEN   = Everything is OK (no action needed)                       │
│  🟡 YELLOW  = Warning (not critical, but should fix soon)               │
│  🔴 RED     = Error (must be fixed for proper operation)                │
│                                                                          │
│  Common Red Errors and Fixes:                                            │
│                                                                          │
│  ✗ Theme Resources Failed                                                │
│    → Reinstall GGs Desktop                                               │
│                                                                          │
│  ✗ .NET Runtime Not Found                                                │
│    → Download .NET 9 from microsoft.com/dotnet                           │
│                                                                          │
│  ✗ Configuration Invalid                                                 │
│    → Delete %LOCALAPPDATA%\GGs\Config\appsettings.json                  │
│    → Restart GGs (new config will be created)                           │
│                                                                          │
│  ✗ Database Connection Failed                                            │
│    → Delete %LOCALAPPDATA%\GGs\Database\ggs.db                          │
│    → Restart GGs (database will be recreated)                           │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘


┌──────────────────────────────────────────────────────────────────────────┐
│  📁 IMPORTANT FILE LOCATIONS                                             │
├──────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  Logs:           %LOCALAPPDATA%\GGs\Logs\desktop.log                    │
│  Configuration:  %LOCALAPPDATA%\GGs\Config\appsettings.json             │
│  Cache:          %LOCALAPPDATA%\GGs\Cache\                               │
│  Diagnostics:    %LOCALAPPDATA%\GGs\Diagnostics\                        │
│  Database:       %LOCALAPPDATA%\GGs\Database\ggs.db                     │
│                                                                          │
│  💡 TIP: Press Win+R, paste the path, press Enter to open folder        │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘


┌──────────────────────────────────────────────────────────────────────────┐
│  ⌨️  KEYBOARD SHORTCUTS                                                  │
├──────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  Ctrl + D        Open diagnostics panel                                  │
│  Ctrl + L        Open log viewer                                         │
│  Ctrl + R        Reload dashboard                                        │
│  F5              Refresh current view                                    │
│  F12             Open developer tools (advanced)                         │
│                                                                          │
│  Alt + Tab       Switch between windows                                  │
│  Alt + Space     Open window menu                                        │
│  Ctrl+Shift+Esc  Open Task Manager                                       │
│  Win + R         Open Run dialog                                         │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘


┌──────────────────────────────────────────────────────────────────────────┐
│  📞 WHEN TO CONTACT SUPPORT                                              │
├──────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  Contact support if:                                                     │
│  ❌ Diagnostics show RED errors you can't fix                           │
│  ❌ Recovery Mode persists after following all steps                    │
│  ❌ Admin Login dialog keeps appearing                                  │
│  ❌ Application crashes repeatedly                                      │
│  ❌ You see error messages not covered in this guide                    │
│                                                                          │
│  What to send to support:                                                │
│  ✅ Screenshot of the error                                             │
│  ✅ Diagnostics report (from %LOCALAPPDATA%\GGs\Diagnostics\)           │
│  ✅ Log file (from %LOCALAPPDATA%\GGs\Logs\desktop.log)                 │
│  ✅ Description of what you were doing when problem occurred            │
│                                                                          │
│  Support Email: support@ggs.example.com                                  │
│  Support Hours: Monday-Friday, 9 AM - 5 PM                               │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘


┌──────────────────────────────────────────────────────────────────────────┐
│  🎯 TROUBLESHOOTING DECISION TREE                                        │
├──────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  START: Launch GGs Desktop                                               │
│     ↓                                                                    │
│  Does window open?                                                       │
│     ├─ NO  → Kill process in Task Manager → Try again                   │
│     └─ YES → Continue                                                    │
│          ↓                                                               │
│       Title says "Recovery Mode"?                                        │
│          ├─ YES → Clear cache → Restart                                 │
│          └─ NO  → Continue                                               │
│               ↓                                                          │
│            Admin login dialog?                                           │
│               ├─ YES → Click Cancel → Report to support                 │
│               └─ NO  → Continue                                          │
│                    ↓                                                     │
│                 Can see dashboard?                                       │
│                    ├─ NO  → Alt+Tab, Alt+Space, M → Move window         │
│                    └─ YES → ✅ WORKING!                                  │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘


┌──────────────────────────────────────────────────────────────────────────┐
│  💡 PRO TIPS                                                             │
├──────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  ✓ Run diagnostics monthly as preventive maintenance                    │
│  ✓ Keep this poster near your computer for quick reference              │
│  ✓ Never enter admin credentials when prompted (just click Cancel)      │
│  ✓ Check log files first before contacting support                      │
│  ✓ Take screenshots of errors to help support diagnose faster           │
│  ✓ Clear cache folder if you see any visual glitches                    │
│  ✓ Restart GGs Desktop after Windows updates                            │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘


╔══════════════════════════════════════════════════════════════════════════╗
║                                                                          ║
║                  📚 FULL DOCUMENTATION AVAILABLE AT:                     ║
║                                                                          ║
║         docs/operator-guides/desktop-troubleshooting-guide.md            ║
║         docs/operator-guides/run-diagnostics-guide.md                    ║
║                                                                          ║
║                    Version 1.0 | Updated 2025-10-04                      ║
║                                                                          ║
╚══════════════════════════════════════════════════════════════════════════╝
```

---

## 🖨️ Printing Instructions

### For Best Results:
1. **Paper**: Use standard 8.5" x 11" (Letter) or A4 paper
2. **Orientation**: Portrait (vertical)
3. **Color**: Color printing recommended (for status indicators)
4. **Quality**: Normal or Draft quality is fine
5. **Margins**: Use default margins

### Black & White Printing:
If printing in black and white:
- 🟢 GREEN = ✓ (checkmark)
- 🟡 YELLOW = ⚠ (warning triangle)
- 🔴 RED = ✗ (X mark)

### Lamination (Optional):
Consider laminating this poster for durability if it will be:
- Posted in a shared workspace
- Handled frequently
- Exposed to spills or moisture

---

## 📌 Where to Post This

**Recommended Locations:**
- ✅ Next to your computer monitor
- ✅ Inside your desk drawer (for quick reference)
- ✅ On office bulletin board (for team access)
- ✅ In your operations manual binder

**Digital Version:**
- Save as PDF for easy sharing
- Email to team members
- Post on internal wiki or SharePoint
- Include in onboarding materials for new operators

---

**Document Version**: 1.0  
**Last Updated**: 2025-10-04  
**Maintained By**: GGs Engineering Team  
**License**: Internal Use Only

