# Keyboard Navigation Test Script

## Phase 8 - Accessibility & Keyboard Navigation Testing

### Test Environment
- **OS**: Windows 10/11
- **Applications**: GGs.Desktop, GGs.ErrorLogViewer
- **Test Date**: 2025-10-02
- **Tester**: _____________

---

## GGs.Desktop - Keyboard Navigation Tests

### 1. Window Controls (TabIndex 1-4)
- [ ] **Tab to Notifications Button** (TabIndex 1)
  - Press Tab from window load
  - Verify focus visual appears
  - Press Enter to open notifications
  
- [ ] **Tab to Theme Toggle** (TabIndex 1)
  - Press Tab
  - Verify focus visual appears
  - Press Enter to toggle theme
  - Verify theme changes immediately

- [ ] **Tab to Minimize** (TabIndex 2)
  - Press Tab
  - Verify focus visual appears
  - Press Enter to minimize window

- [ ] **Tab to Maximize** (TabIndex 3)
  - Press Tab
  - Verify focus visual appears
  - Press Enter to maximize/restore window

- [ ] **Tab to Close** (TabIndex 4)
  - Press Tab
  - Verify focus visual appears (red background on hover)
  - Press Escape to cancel (don't close)

### 2. Navigation Rail (TabIndex 10-17)
- [ ] **Tab to Dashboard** (TabIndex 10)
  - Press Tab to reach navigation
  - Verify focus visual appears
  - Press Enter to navigate to Dashboard

- [ ] **Tab to Optimization** (TabIndex 11)
  - Press Tab
  - Press Enter to navigate to Optimization view

- [ ] **Tab to Network** (TabIndex 12)
  - Press Tab
  - Press Enter to navigate to Network view

- [ ] **Tab to Monitoring** (TabIndex 13)
  - Press Tab
  - Press Enter to navigate to Monitoring view

- [ ] **Tab to Profiles** (TabIndex 14)
  - Press Tab
  - Press Enter to navigate to Profiles view

- [ ] **Tab to System Intelligence** (TabIndex 15)
  - Press Tab
  - Press Enter to navigate to System Intelligence view

- [ ] **Tab to Notifications** (TabIndex 16)
  - Press Tab
  - Press Enter to navigate to Notifications view

- [ ] **Tab to Settings** (TabIndex 17)
  - Press Tab
  - Press Enter to navigate to Settings view

### 3. Dashboard View - Interactive Elements
- [ ] **Quick Optimize Button**
  - Tab to button
  - Press Enter to trigger optimization
  - Verify feedback message appears

- [ ] **EliBot Question Box**
  - Tab to text box
  - Type question
  - Tab to "Ask" button
  - Press Enter to submit

- [ ] **Quick Action Cards**
  - Tab to "Game Mode" button
  - Press Enter to activate
  - Tab to "Boost" button
  - Press Enter to boost
  - Tab to "Clean" button
  - Press Enter to clean
  - Tab to "Silent Mode" button
  - Press Enter to enable

### 4. Settings View - Form Controls
- [ ] **Theme ComboBox**
  - Tab to theme dropdown
  - Press Down Arrow to open
  - Use Arrow Keys to select theme
  - Press Enter to confirm

- [ ] **Font Size Slider**
  - Tab to slider
  - Use Arrow Keys to adjust (Left/Right or Up/Down)
  - Verify font size changes in real-time

- [ ] **Accent Color TextBoxes**
  - Tab to primary accent textbox
  - Type hex color (e.g., #00FFFF)
  - Tab to secondary accent textbox
  - Type hex color

- [ ] **Checkboxes**
  - Tab to "Start with Windows" checkbox
  - Press Space to toggle
  - Tab to "Launch minimized" checkbox
  - Press Space to toggle

- [ ] **Server URL TextBox**
  - Tab to server URL textbox
  - Type URL
  - Tab to "Save" button
  - Press Enter to save

---

## GGs.ErrorLogViewer - Keyboard Navigation Tests

### 1. Hero Section - Action Buttons (TabIndex 10-14)
- [ ] **Tab to Start Monitoring** (TabIndex 10)
  - Press Tab from window load
  - Verify focus visual appears
  - Press Enter to start monitoring

- [ ] **Tab to Stop** (TabIndex 11)
  - Press Tab
  - Press Enter to stop monitoring

- [ ] **Tab to Refresh** (TabIndex 12)
  - Press Tab
  - Press Enter to refresh logs

- [ ] **Tab to Clear** (TabIndex 13)
  - Press Tab
  - Press Enter to clear logs (confirm dialog)

- [ ] **Tab to Export** (TabIndex 14)
  - Press Tab
  - Press Enter to open export menu

### 2. Filter Bar - Search & Filters (TabIndex 1-7)
- [ ] **Tab to Search Box** (TabIndex 1)
  - Press Tab
  - Type search query
  - Verify real-time filtering

- [ ] **Tab to Log Level Filter** (TabIndex 2)
  - Press Tab
  - Press Down Arrow to open dropdown
  - Use Arrow Keys to select level (Error, Warning, Info, etc.)
  - Press Enter to confirm
  - Verify logs filter immediately

- [ ] **Tab to Source Filter** (TabIndex 3)
  - Press Tab
  - Press Down Arrow to open dropdown
  - Use Arrow Keys to select source
  - Press Enter to confirm

- [ ] **Tab to Regex Toggle** (TabIndex 4)
  - Press Tab
  - Press Space to toggle regex mode
  - Verify toggle state changes

- [ ] **Tab to Smart Filter Toggle** (TabIndex 5)
  - Press Tab
  - Press Space to toggle smart filter
  - Verify deduplication applies

- [ ] **Tab to Auto Scroll Toggle** (TabIndex 6)
  - Press Tab
  - Press Space to toggle auto scroll
  - Verify auto-scroll behavior

- [ ] **Tab to Raw Mode Toggle** (TabIndex 7)
  - Press Tab
  - Press Space to toggle raw/compact view
  - Verify message display changes

### 3. Navigation Panel (TabIndex 20-26)
- [ ] **Tab to Live Logs** (TabIndex 20)
  - Press Tab to reach navigation
  - Press Enter to switch to Logs view

- [ ] **Tab to Analytics** (TabIndex 21)
  - Press Tab
  - Press Enter to switch to Analytics view
  - Verify stat cards display

- [ ] **Tab to Bookmarks** (TabIndex 22)
  - Press Tab
  - Press Enter to switch to Bookmarks view
  - Verify bookmarks list displays

- [ ] **Tab to Smart Alerts** (TabIndex 23)
  - Press Tab
  - Press Enter to switch to Alerts view
  - Verify alerts display

- [ ] **Tab to Compare Runs** (TabIndex 24)
  - Press Tab
  - Press Enter to switch to Compare view

- [ ] **Tab to Exports** (TabIndex 25)
  - Press Tab
  - Press Enter to switch to Exports view

- [ ] **Tab to Settings** (TabIndex 26)
  - Press Tab
  - Press Enter to switch to Settings view

### 4. DataGrid Navigation
- [ ] **Tab to DataGrid**
  - Press Tab to focus DataGrid
  - Use Arrow Keys to navigate rows (Up/Down)
  - Use Arrow Keys to navigate columns (Left/Right)
  - Press Enter to expand details
  - Press Escape to collapse details

- [ ] **DataGrid Context Menu**
  - Right-click on row (or press Context Menu key)
  - Use Arrow Keys to navigate menu
  - Press Enter to select action

### 5. Analytics View - Interactive Elements
- [ ] **Refresh Analytics Button**
  - Tab to button
  - Press Enter to refresh
  - Verify stats update

- [ ] **Detect Anomalies Button**
  - Tab to button
  - Press Enter to detect
  - Verify anomalies highlighted

### 6. Bookmarks View - Interactive Elements
- [ ] **Add Bookmark Button**
  - Select log entry in DataGrid
  - Tab to "Add Bookmark" button
  - Press Enter to add
  - Verify bookmark appears in list

- [ ] **Go To Bookmark Button**
  - Tab to bookmark card
  - Tab to "Go To" button
  - Press Enter to navigate
  - Verify log entry selected in DataGrid

- [ ] **Remove Bookmark Button**
  - Tab to "Remove" button
  - Press Enter to remove
  - Verify bookmark removed from list

### 7. Alerts View - Interactive Elements
- [ ] **Create Alert Button**
  - Tab to "Create Alert" button
  - Press Enter to open dialog
  - Fill in alert details
  - Press Enter to save

- [ ] **Acknowledge Alert Button**
  - Tab to triggered alert card
  - Tab to "Acknowledge" button
  - Press Enter to acknowledge
  - Verify alert removed from triggered list

---

## Accessibility Tests

### High Contrast Mode
- [ ] **Enable Windows High Contrast**
  - Press Left Alt + Left Shift + Print Screen
  - Verify all text is readable
  - Verify all buttons have visible borders
  - Verify focus visuals are clearly visible
  - Verify color-coded elements have text labels

### Screen Reader (Narrator)
- [ ] **Enable Narrator**
  - Press Ctrl + Win + Enter
  - Tab through all controls
  - Verify each control announces its name and role
  - Verify AutomationProperties.Name is read correctly
  - Verify button states are announced (pressed/not pressed)

### Focus Visuals
- [ ] **Verify Focus Indicators**
  - Tab through all interactive elements
  - Verify each element shows clear focus visual
  - Verify focus visual has sufficient contrast (3:1 minimum)
  - Verify focus visual is not obscured by other elements

---

## Performance Tests

### UI Responsiveness
- [ ] **No UI Blocking >100ms**
  - Perform heavy operations (load 10,000 logs)
  - Verify UI remains responsive during load
  - Verify no freezing or stuttering
  - Verify progress indicators show during long operations

### DataGrid Virtualization
- [ ] **Large Dataset Performance**
  - Load 50,000+ log entries
  - Scroll through DataGrid
  - Verify smooth scrolling (60 FPS)
  - Verify memory usage remains stable

### Theme Switching
- [ ] **Theme Toggle Performance**
  - Toggle between themes rapidly
  - Verify instant theme change (<50ms)
  - Verify no visual glitches
  - Verify theme persists across restarts

---

## Test Results Summary

**Total Tests**: 80+
**Passed**: _____
**Failed**: _____
**Blocked**: _____

### Critical Issues Found
1. _____________________________________________
2. _____________________________________________
3. _____________________________________________

### Recommendations
1. _____________________________________________
2. _____________________________________________
3. _____________________________________________

---

## Sign-Off

**Tester Name**: _____________
**Date**: _____________
**Signature**: _____________

**Approved By**: _____________
**Date**: _____________
**Signature**: _____________

