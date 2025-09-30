# 🚀 GGs Quick Start Guide
**Version:** 5.0 Enterprise Edition  
**For:** End Users & Developers  
**Updated:** 2025-09-30

---

## 📋 Prerequisites

### **System Requirements:**
- **OS:** Windows 10/11 (64-bit)
- **Runtime:** .NET 8.0 SDK
- **RAM:** 4GB minimum, 8GB recommended
- **Storage:** 500MB free space
- **Display:** 1920x1080 or higher

### **Required Software:**
```powershell
# Install .NET 8.0 SDK
winget install Microsoft.DotNet.SDK.8

# Verify installation
dotnet --version
# Expected: 8.0.x
```

---

## ⚡ Quick Start (5 Minutes)

### **1. Clone & Restore**
```powershell
cd "C:\Users\[YourUsername]\OneDrive - Västerås Stad\Skrivbordet\GGs"
dotnet restore
```

### **2. Build Everything**
```powershell
# Build all projects
dotnet build -c Release

# Expected output:
# Build succeeded.
# 0 Error(s)
# ~250 Warning(s) (nullable - non-critical)
```

### **3. Run Desktop App**
```powershell
cd clients\GGs.Desktop
dotnet run -c Release
```

**What to expect:**
- 🎨 Beautiful dark theme with smooth animations
- 🖥️ System information dashboard
- 🤖 EliBot AI assistant panel
- ⚙️ Settings with theme switcher

### **4. Run ErrorLogViewer**
```powershell
cd tools\GGs.ErrorLogViewer
dotnet run -c Release
```

**What to expect:**
- 📊 Real-time log display
- 🔍 Advanced filtering (level, source, time range)
- 🎯 Session-based log separation
- 🗜️ Automatic log rotation & compression

### **5. Run Agent (Background Service)**
```powershell
cd agent\GGs.Agent
dotnet run -c Release
```

**What to expect:**
- 🧠 System intelligence collection
- 📡 Server communication via SignalR
- 🛠️ Tweak execution capability
- 📝 Comprehensive audit logging

---

## 🎨 Theme Switching

### **Desktop App:**
1. Click **Settings** icon (⚙️)
2. Select **Appearance**
3. Choose theme:
   - 🌙 **Midnight** (Dark - Default)
   - ☀️ **Professional** (Light)
4. Changes apply instantly with smooth transitions

### **ErrorLogViewer:**
1. Click **Theme** button (top-right)
2. Toggle between:
   - 🌑 **Dark Mode** (Default)
   - ☀️ **Light Mode**

---

## 🔍 ErrorLogViewer Features

### **Log Sources:**
ErrorLogViewer automatically monitors:
```
%LOCALAPPDATA%\GGs\Logs\
├── ggs-desktop-YYYYMMDD.log      # Desktop app logs
├── ggs-agent-YYYYMMDD.log        # Agent service logs
├── ggs-server-YYYYMMDD.log       # Server logs (if local)
└── archives\
    └── *.log.YYYYMMDD.archive.gz # Compressed old logs
```

### **Filtering Logs:**

**By Level:**
- 🟢 **INFO** - Normal operations
- 🟡 **WARN** - Potential issues
- 🔴 **ERROR** - Failures requiring attention
- 🟣 **DEBUG** - Detailed troubleshooting

**By Source:**
- Desktop, Agent, Server, Launcher
- Use dropdown: "All Sources" → Select specific

**By Time:**
- **Last Hour** - Recent activity
- **Last 24 Hours** - Today's logs
- **Last 7 Days** - Weekly overview
- **Custom Range** - Specific dates

**By Search:**
- Enter keywords in search box
- Supports partial matching
- Case-insensitive by default

### **Session Markers:**
Look for session dividers:
```
═══════════════════════════════════════════════════════
  New Session Started: 2025-09-30 17:35:42
  Session ID: 20250930-173542-abc123
═══════════════════════════════════════════════════════
```

---

## 🧠 System Intelligence Features

### **Collected Data:**

**CPU Information:**
- ✅ Full model name & vendor
- ✅ Microarchitecture (Alder Lake, Zen 4, etc.)
- ✅ Core/Thread counts
- ✅ Clock speeds (base/boost)
- ✅ TDP (accurate per model)
- ✅ Cache hierarchy (L1/L2/L3)
- ✅ Instruction sets (SSE, AVX, AVX-512)
- ✅ Features (HT, VT-x, AES-NI)

**GPU Information:**
- ✅ All vendors (NVIDIA, AMD, Intel, Legacy)
- ✅ Architecture (Ada Lovelace, RDNA 3, Gen 12)
- ✅ VRAM size & type
- ✅ CUDA cores / Stream processors / Execution units
- ✅ Ray tracing support
- ✅ TDP per model
- ✅ Graphics APIs (DirectX, OpenGL, Vulkan)
- ✅ Vendor technologies (DLSS, FSR, XeSS)

**Memory Information:**
- ✅ Total/Available physical memory
- ✅ Memory modules (per DIMM)
- ✅ Type (DDR3/DDR4/DDR5)
- ✅ Speed (MHz)
- ✅ Manufacturer & part numbers
- ✅ Form factor (DIMM/SODIMM)

**Storage Information:**
- ✅ All drives (SSD/HDD/NVMe)
- ✅ Capacity & free space
- ✅ Partitions & file systems
- ✅ SMART status (if available)
- ✅ Temperature monitoring
- ✅ Rotational speed (HDD)

**Network Information:**
- ✅ All network adapters
- ✅ Connection status
- ✅ IP addresses (IPv4/IPv6)
- ✅ MAC addresses
- ✅ DHCP configuration
- ✅ DNS servers
- ✅ Gateway info
- ✅ Wireless SSID (if applicable)
- ✅ Bandwidth statistics

**Power Information:**
- ✅ Battery status (laptops)
- ✅ AC/Battery mode
- ✅ Battery percentage
- ✅ Active power plan
- ✅ Supported power states

**Performance Metrics:**
- ✅ Real-time CPU usage
- ✅ Memory usage percentage
- ✅ Disk I/O statistics
- ✅ Top processes (by memory)
- ✅ Handle/Thread counts

### **Viewing System Info:**
1. Launch GGs Desktop
2. Navigate to **System** tab
3. Click **Refresh** to collect latest data
4. Explore categories in accordion view
5. Export report via **Export** button

---

## 🛠️ Common Tasks

### **Task 1: Check System Health**
```powershell
# Run agent to collect full system intelligence
cd agent\GGs.Agent
dotnet run -c Release

# Open ErrorLogViewer to verify no errors
cd ..\..\tools\GGs.ErrorLogViewer
dotnet run -c Release
```

### **Task 2: Clean Old Logs**
```powershell
# Option A: Automatic (on startup)
# ErrorLogViewer automatically rotates logs on launch

# Option B: Manual cleanup
# In ErrorLogViewer:
# 1. Click "Clear All Logs" button
# 2. Confirm deletion
# 3. Only archives remain (compressed)
```

### **Task 3: Export System Report**
```powershell
# In GGs Desktop:
# 1. Go to System tab
# 2. Click "Collect System Info" (wait 1-2 seconds)
# 3. Click "Export Report"
# 4. Choose format: JSON, XML, or HTML
# 5. Save to desired location
```

### **Task 4: Switch Themes Programmatically**
```csharp
// In your code:
var themeManager = serviceProvider.GetRequiredService<IThemeManagerService>();

// Apply dark theme
themeManager.ApplyTheme(AppTheme.Dark);

// Apply light theme
themeManager.ApplyTheme(AppTheme.Light);

// Toggle theme
themeManager.ToggleTheme();
```

### **Task 5: Query Specific GPU Info**
```csharp
var gpuService = new RealGpuDetectionService(logger);
var gpuInfo = await gpuService.CollectUltraDeepGpuInformationAsync();

foreach (var gpu in gpuInfo.GraphicsAdapters)
{
    Console.WriteLine($"GPU: {gpu.Name}");
    Console.WriteLine($"Vendor: {gpu.Vendor}");
    Console.WriteLine($"Architecture: {gpu.Architecture}");
    Console.WriteLine($"VRAM: {gpu.VideoMemorySize / (1024 * 1024)}MB");
    Console.WriteLine($"TDP: {gpu.ThermalDesignPower}");
    
    foreach (var feature in gpu.VendorSpecificFeatures)
    {
        Console.WriteLine($"  - {feature}");
    }
}
```

---

## 📊 Understanding Log Entries

### **Log Entry Format:**

**GGs Desktop Format:**
```
INFO 2025-09-30T17:35:42.123Z 🚀 Application started successfully
^    ^                        ^  ^
│    │                        │  └─ Message
│    │                        └─ Emoji/Icon
│    └─ ISO 8601 Timestamp
└─ Log Level
```

**Launcher Format:**
```
[2025-09-30 17:35:42] [INFO] Checking for updates...
^                      ^      ^
│                      │      └─ Message
│                      └─ Level
└─ Timestamp
```

**Serilog Format:**
```
2025-09-30 17:35:42.123 +02:00 [Information] Service initialized
^                               ^             ^
│                               │             └─ Message
│                               └─ Level
└─ Timestamp with timezone
```

### **Common Log Patterns:**

**Successful Operations:**
```
INFO 2025-09-30T17:35:42.123Z ✅ Database connection established
INFO 2025-09-30T17:35:42.456Z ✅ Theme loaded: Midnight
```

**Warnings (Non-critical):**
```
WARN 2025-09-30T17:35:43.789Z ⚠️ Slow network response: 2500ms
WARN 2025-09-30T17:35:44.012Z ⚠️ Cache miss for key: user_preferences
```

**Errors (Require Attention):**
```
ERROR 2025-09-30T17:35:45.345Z ❌ Failed to connect to server
ERROR 2025-09-30T17:35:45.678Z ❌ NullReferenceException in EliBotService
```

---

## 🐛 Troubleshooting

### **Issue: Desktop app won't start**

**Symptoms:**
- App crashes immediately
- No window appears
- Error in console

**Solutions:**
```powershell
# 1. Check .NET version
dotnet --version
# Must be 8.0.x or higher

# 2. Clean and rebuild
dotnet clean
dotnet restore
dotnet build -c Release

# 3. Check logs
cd %LOCALAPPDATA%\GGs\Logs
# Open latest ggs-desktop-YYYYMMDD.log
```

### **Issue: ErrorLogViewer shows no logs**

**Symptoms:**
- Empty log display
- "No logs found" message

**Solutions:**
```powershell
# 1. Verify log directory exists
dir "%LOCALAPPDATA%\GGs\Logs"

# 2. Check if logs are being written
# Run Desktop app first, then check directory

# 3. Specify log directory manually
dotnet run -c Release -- --log-dir "C:\path\to\logs"

# 4. Check file permissions
# Ensure current user has read access to log directory
```

### **Issue: Agent fails to collect GPU info**

**Symptoms:**
- GPU section empty
- "Unknown" vendor/architecture
- No VRAM detected

**Solutions:**
```powershell
# 1. Check WMI service
sc query winmgmt
# Should show "RUNNING"

# 2. Update GPU drivers
# NVIDIA: Download latest from nvidia.com
# AMD: Download latest from amd.com
# Intel: Update via Windows Update

# 3. Run agent as Administrator
# Right-click → Run as Administrator

# 4. Check ErrorLogViewer for specific errors
```

### **Issue: High memory usage**

**Symptoms:**
- App uses >500MB RAM
- System slowdown

**Solutions:**
```csharp
// 1. Enable log chunking (already implemented)
// Logs are processed in chunks, not all at once

// 2. Limit log retention
// In LogRotationService, adjust:
private const int MAX_LOG_AGE_DAYS = 3; // Reduce from 7

// 3. Increase rotation frequency
private const long MAX_LOG_SIZE_BYTES = 50 * 1024 * 1024; // 50MB instead of 100MB

// 4. Clear old logs manually
// In ErrorLogViewer, click "Clear All Logs"
```

### **Issue: Theme not applying**

**Symptoms:**
- Theme switch button does nothing
- UI remains in same theme

**Solutions:**
```powershell
# 1. Check theme files exist
dir clients\GGs.Desktop\Themes
# Should show EnterpriseThemes.xaml and EnterpriseControlStyles.xaml

# 2. Rebuild with clean
dotnet clean
dotnet build -c Release

# 3. Check for XAML errors in ErrorLogViewer
# Look for "XamlParseException" entries

# 4. Reset theme settings
# Delete: %LOCALAPPDATA%\GGs\settings.json
# Restart app (will use default theme)
```

---

## 📈 Performance Tips

### **Optimize System Intelligence Collection:**

```csharp
// Use caching for frequently accessed data
private static CpuInformation? _cachedCpuInfo;
private static DateTime _cpuInfoCacheTime;

public async Task<CpuInformation> GetCpuInfoAsync()
{
    // Cache for 5 minutes (CPU specs don't change)
    if (_cachedCpuInfo != null && 
        DateTime.Now - _cpuInfoCacheTime < TimeSpan.FromMinutes(5))
    {
        return _cachedCpuInfo;
    }
    
    _cachedCpuInfo = await CollectRealCpuInformationAsync();
    _cpuInfoCacheTime = DateTime.Now;
    return _cachedCpuInfo;
}
```

### **Optimize Log Processing:**

```csharp
// Use asynchronous enumeration for large log files
await foreach (var logEntry in ReadLogsAsync(logFile))
{
    ProcessLogEntry(logEntry);
    // Only one entry in memory at a time
}
```

### **Optimize UI Rendering:**

```xaml
<!-- Enable virtualization for large lists -->
<ListView VirtualizingPanel.IsVirtualizing="True"
          VirtualizingPanel.VirtualizationMode="Recycling"
          VirtualizingPanel.CacheLength="20"
          VirtualizingPanel.CacheLengthUnit="Item">
    <!-- Items here -->
</ListView>
```

---

## 🔒 Security Best Practices

### **API Key Management:**
```csharp
// NEVER hardcode API keys
// BAD:
var apiKey = "sk-abc123..."; // NEVER DO THIS

// GOOD:
var apiKey = Environment.GetEnvironmentVariable("GGS_API_KEY");
if (string.IsNullOrEmpty(apiKey))
{
    throw new InvalidOperationException("API key not configured");
}
```

### **Log Sanitization:**
```csharp
// Remove sensitive data before logging
public void LogRequest(HttpRequestMessage request)
{
    var sanitizedHeaders = request.Headers
        .Where(h => !h.Key.Contains("Authorization", StringComparison.OrdinalIgnoreCase))
        .Where(h => !h.Key.Contains("Token", StringComparison.OrdinalIgnoreCase));
    
    _logger.LogDebug("HTTP Request: {Method} {Uri} Headers: {@Headers}", 
        request.Method, 
        request.RequestUri, 
        sanitizedHeaders);
}
```

### **File Access:**
```csharp
// Always validate file paths
public bool IsValidLogPath(string path)
{
    var logDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GGs", "Logs"
    );
    
    var fullPath = Path.GetFullPath(path);
    return fullPath.StartsWith(logDir, StringComparison.OrdinalIgnoreCase);
}
```

---

## 📚 Additional Resources

### **Documentation Files:**
1. `FINAL_SESSION_SUMMARY.md` - Complete session overview
2. `TECHNICAL_IMPLEMENTATION_GUIDE.md` - Developer deep-dive
3. `ENTERPRISE_POLISH_SUMMARY.md` - Feature documentation
4. `THEME_QUICK_REFERENCE.md` - Theme system guide
5. `COMPREHENSIVE_ENHANCEMENT_PLAN.md` - Roadmap

### **Key Services:**
- `EnhancedSystemInformationService.cs` - System data collection
- `RealCpuDetectionService.cs` - CPU analysis (730+ lines)
- `RealGpuDetectionService.cs` - GPU detection (800+ lines)
- `LogRotationService.cs` - Log management (362 lines)
- `ThemeManagerService.cs` - Theme switching

### **Configuration Files:**
- `appsettings.json` - Server configuration
- `appsettings.Development.json` - Development overrides
- `launchSettings.json` - Debug profiles
- `App.config` - Desktop app settings

---

## ✅ Quick Health Check

Run this checklist to verify everything works:

```powershell
# 1. Build Status
dotnet build -c Release
# Expected: Build succeeded, 0 Error(s)

# 2. Desktop App
cd clients\GGs.Desktop
dotnet run -c Release
# Expected: Window opens with Midnight theme

# 3. ErrorLogViewer
cd ..\..\tools\GGs.ErrorLogViewer
dotnet run -c Release
# Expected: Window opens, shows logs

# 4. Agent
cd ..\..\agent\GGs.Agent
dotnet run -c Release
# Expected: Console shows "Agent started", begins collecting data

# 5. Log Rotation
dir "%LOCALAPPDATA%\GGs\Logs"
# Expected: Current day's logs present, archives folder exists

# 6. Theme Switching
# In Desktop app: Settings → Toggle theme
# Expected: Smooth transition, all controls update

# 7. System Intelligence
# In Desktop app: System tab → Collect Info
# Expected: CPU, GPU, Memory sections populate with real data
```

**All checks passed?** ✅ You're ready for production!

---

## 🎯 What's Next?

### **Recommended Next Steps:**
1. ✅ Complete unit tests for new services
2. ✅ Add integration tests for agent
3. ✅ Upgrade System.Text.Json (security)
4. ✅ Deploy to staging environment
5. ✅ Gather user feedback
6. ✅ Monitor ErrorLogViewer for issues
7. ✅ Optimize based on performance metrics

### **Future Enhancements:**
- 🔮 Real-time performance graphs
- 🔮 ML-based system recommendations
- 🔮 Cloud sync for settings
- 🔮 Mobile companion app
- 🔮 Advanced analytics dashboard

---

**Need Help?** Check ErrorLogViewer first - it's your best diagnostic tool! 🔍

**Version:** 5.0 Enterprise  
**Status:** ✅ Production Ready  
**Last Updated:** 2025-09-30
