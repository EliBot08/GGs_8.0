# GGs Enterprise - Test Checklist
## Phase 10 - Test Strategy (Builds, Smokes, Functionals)

### Test Environment
- **OS**: Windows 10/11
- **Test Date**: _______________
- **Tester**: _______________
- **Build Configuration**: Debug / Release

---

## 1. Build Tests

### 1.1 Debug Build
- [ ] **Clean Build**
  ```bash
  dotnet clean GGs.sln --configuration Debug
  dotnet build GGs.sln --configuration Debug
  ```
  - Expected: Build succeeds with 0 errors, 0 warnings
  - Actual: _______________

- [ ] **Incremental Build**
  ```bash
  dotnet build GGs.sln --configuration Debug --no-restore
  ```
  - Expected: Build succeeds in <5 seconds
  - Actual: _______________

### 1.2 Release Build
- [ ] **Clean Build**
  ```bash
  dotnet clean GGs.sln --configuration Release
  dotnet build GGs.sln --configuration Release
  ```
  - Expected: Build succeeds with 0 errors, 0 warnings
  - Actual: _______________

- [ ] **Optimizations Enabled**
  - Verify Release build is optimized
  - Check bin/Release folders for optimized DLLs
  - Expected: File sizes smaller than Debug
  - Actual: _______________

---

## 2. UI Smoke Tests

### 2.1 GGs.Desktop - Launch & Welcome
- [ ] **First Launch**
  - Delete `%LocalAppData%\GGs\settings.json`
  - Launch GGs.Desktop.exe
  - Expected: Welcome overlay appears with "GGs PRO" logo
  - Expected: Progress bar shows initialization steps
  - Expected: First-run checklist visible
  - Actual: _______________

- [ ] **Subsequent Launch**
  - Close and relaunch application
  - Expected: No welcome overlay
  - Expected: Dashboard view loads immediately
  - Actual: _______________

### 2.2 GGs.Desktop - Navigation
- [ ] **Navigate to All Views**
  - Click Dashboard → Verify stats cards display
  - Click Optimization → Verify optimization view loads
  - Click Network → Verify network view loads
  - Click Monitoring → Verify monitoring view loads
  - Click Profiles → Verify profiles view loads
  - Click System Intelligence → Verify tabs display
  - Click Notifications → Verify notifications view loads
  - Click Settings → Verify settings form loads
  - Expected: All views load without errors
  - Actual: _______________

### 2.3 GGs.Desktop - Theme Toggle
- [ ] **Toggle Theme**
  - Click theme toggle button in title bar
  - Expected: Theme changes immediately (Midnight ↔ Vapor ↔ Tactical ↔ Carbon ↔ Light)
  - Expected: All colors update across entire UI
  - Actual: _______________

- [ ] **Theme Persistence**
  - Toggle to Vapor theme
  - Close application
  - Relaunch application
  - Expected: Vapor theme persists
  - Actual: _______________

### 2.4 GGs.ErrorLogViewer - Launch & Welcome
- [ ] **First Launch**
  - Delete first-run flag file
  - Launch GGs.ErrorLogViewer.exe
  - Expected: Quick actions banner appears
  - Expected: "Open Logs Folder" and "Import Sample Log" buttons visible
  - Actual: _______________

- [ ] **Import Sample Logs**
  - Click "Import Sample Log" button
  - Expected: 4 sample log entries appear in DataGrid
  - Expected: Entries have different levels (INFO, WARN, ERROR, SUCCESS)
  - Actual: _______________

### 2.5 GGs.ErrorLogViewer - Navigation
- [ ] **Navigate to All Views**
  - Click Live Logs → Verify DataGrid displays
  - Click Analytics → Verify stat cards and charts display
  - Click Bookmarks → Verify bookmarks list displays
  - Click Smart Alerts → Verify alerts display
  - Click Compare Runs → Verify compare view loads
  - Click Exports → Verify export view loads
  - Click Settings → Verify settings form loads
  - Expected: All views load without errors
  - Actual: _______________

---

## 3. ErrorLogViewer Functional Tests

### 3.1 Regex Search
- [ ] **Basic Search**
  - Type "error" in search box
  - Expected: Only entries containing "error" display
  - Actual: _______________

- [ ] **Regex Search**
  - Enable "Regex" toggle
  - Type `^ERROR.*database` in search box
  - Expected: Only ERROR entries starting with "database" display
  - Actual: _______________

- [ ] **Invalid Regex**
  - Type `[invalid` in search box
  - Expected: Error message or no crash
  - Actual: _______________

### 3.2 Level/Source Filter
- [ ] **Filter by Level**
  - Select "Error" from level dropdown
  - Expected: Only ERROR level entries display
  - Expected: Entry count updates
  - Actual: _______________

- [ ] **Filter by Source**
  - Select a source from source dropdown
  - Expected: Only entries from that source display
  - Actual: _______________

- [ ] **Combined Filters**
  - Select "Error" level + specific source
  - Expected: Only ERROR entries from that source display
  - Actual: _______________

### 3.3 Raw/Compact Toggle
- [ ] **Toggle Raw Mode**
  - Click "Raw" toggle button
  - Expected: Messages display in full raw format
  - Actual: _______________

- [ ] **Toggle Compact Mode**
  - Click "Raw" toggle button again
  - Expected: Messages display in compact format
  - Actual: _______________

### 3.4 Export CSV/JSON
- [ ] **Export to CSV**
  - Click "Export" button
  - Select "Export to CSV"
  - Choose save location
  - Expected: CSV file created with all filtered entries
  - Expected: Success message displays
  - Actual: _______________

- [ ] **Export to JSON**
  - Click "Export" button
  - Select "Export to JSON"
  - Choose save location
  - Expected: JSON file created with pretty-print formatting
  - Expected: Success message displays
  - Actual: _______________

- [ ] **Export with Filters**
  - Apply level filter (Error only)
  - Export to CSV
  - Expected: Only ERROR entries in CSV
  - Actual: _______________

### 3.5 Details Expanders
- [ ] **Expand Row Details**
  - Click on a log entry row
  - Expected: Details panel expands below row
  - Expected: Full message, stack trace, and metadata display
  - Actual: _______________

- [ ] **Collapse Row Details**
  - Click on expanded row again
  - Expected: Details panel collapses
  - Actual: _______________

### 3.6 Bookmarks
- [ ] **Add Bookmark**
  - Select a log entry
  - Navigate to Bookmarks view
  - Click "Add Bookmark" button
  - Expected: Bookmark appears in list
  - Expected: Bookmark shows title, message, timestamp
  - Actual: _______________

- [ ] **Go To Bookmark**
  - Click "Go To" button on bookmark
  - Expected: Navigates to Live Logs view
  - Expected: Log entry is selected in DataGrid
  - Actual: _______________

- [ ] **Remove Bookmark**
  - Click "Remove" button on bookmark
  - Expected: Bookmark removed from list
  - Actual: _______________

### 3.7 Alerts
- [ ] **Create Alert**
  - Navigate to Smart Alerts view
  - Click "Create Alert" button
  - Enter alert name and pattern
  - Expected: Alert appears in configured rules list
  - Actual: _______________

- [ ] **Trigger Alert**
  - Import logs matching alert pattern
  - Expected: Alert appears in triggered alerts section
  - Expected: Match count displays
  - Actual: _______________

- [ ] **Acknowledge Alert**
  - Click "Acknowledge" button on triggered alert
  - Expected: Alert removed from triggered list
  - Actual: _______________

---

## 4. Launcher Tests

### 4.1 Launch-ErrorLogViewer.bat
- [ ] **Normal Launch (Debug)**
  ```bash
  Launch-ErrorLogViewer.bat
  ```
  - Expected: Cleans, builds, launches ErrorLogViewer
  - Expected: Log file created in launcher-logs/
  - Actual: _______________

- [ ] **Release Build**
  ```bash
  Launch-ErrorLogViewer.bat --release
  ```
  - Expected: Builds in Release configuration
  - Expected: Launches Release exe
  - Actual: _______________

- [ ] **No Restore**
  ```bash
  Launch-ErrorLogViewer.bat --no-restore
  ```
  - Expected: Skips restore step
  - Expected: Build succeeds if dependencies already restored
  - Actual: _______________

- [ ] **No Launch**
  ```bash
  Launch-ErrorLogViewer.bat --no-launch
  ```
  - Expected: Builds but doesn't launch
  - Expected: Success message displays
  - Actual: _______________

### 4.2 Launch-Desktop.bat
- [ ] **Normal Launch (Debug)**
  ```bash
  Launch-Desktop.bat
  ```
  - Expected: Cleans, builds, launches Desktop
  - Actual: _______________

- [ ] **With Server**
  ```bash
  Launch-Desktop.bat --with-server
  ```
  - Expected: Starts local server automatically
  - Expected: Desktop connects to localhost:5000
  - Actual: _______________

### 4.3 Launch-All.bat
- [ ] **Normal Launch**
  ```bash
  Launch-All.bat
  ```
  - Expected: Cleans and builds server, desktop, viewer
  - Expected: Runs solution tests
  - Expected: Launches both desktop and viewer
  - Actual: _______________

- [ ] **Skip Tests**
  ```bash
  Launch-All.bat --skip-tests
  ```
  - Expected: Skips test execution
  - Expected: Launches apps faster
  - Actual: _______________

### 4.4 Failure Mode Tests
- [ ] **Missing SDK**
  - Temporarily rename dotnet.exe
  - Run launcher
  - Expected: Clear error message about missing SDK
  - Expected: Non-zero exit code
  - Actual: _______________

- [ ] **Build Error**
  - Introduce syntax error in code
  - Run launcher
  - Expected: Build fails with clear error
  - Expected: Log file contains error details
  - Expected: Non-zero exit code
  - Actual: _______________

- [ ] **Missing Executable**
  - Delete bin folder
  - Run launcher with --no-restore
  - Expected: Clear error about missing exe
  - Expected: Non-zero exit code
  - Actual: _______________

---

## 5. Enforcement Tests

### 5.1 Placeholder Scan
- [ ] **Scan for Placeholders**
  ```bash
  findstr /s /i "TODO FIXME PLACEHOLDER XXX" *.cs *.xaml
  ```
  - Expected: No critical placeholders in production code
  - Actual: _______________

### 5.2 Garbled Glyph Scan
- [ ] **Scan for Garbled Unicode**
  ```bash
  findstr /s /r "[\xC0-\xFF]" *.xaml
  ```
  - Expected: No garbled characters in XAML
  - Actual: _______________

### 5.3 Binding Error Check
- [ ] **Run Application with Debugger**
  - Launch GGs.Desktop in Visual Studio
  - Navigate through all views
  - Check Output window for binding errors
  - Expected: No binding errors
  - Actual: _______________

- [ ] **Run ErrorLogViewer with Debugger**
  - Launch GGs.ErrorLogViewer in Visual Studio
  - Navigate through all views
  - Check Output window for binding errors
  - Expected: No binding errors
  - Actual: _______________

---

## Test Results Summary

**Total Tests**: 70+
**Passed**: _____
**Failed**: _____
**Blocked**: _____

### Critical Issues
1. _______________________________________________
2. _______________________________________________
3. _______________________________________________

### Recommendations
1. _______________________________________________
2. _______________________________________________
3. _______________________________________________

---

## Sign-Off

**Tester**: _______________
**Date**: _______________
**Status**: PASS / FAIL / BLOCKED

