# Next Steps (Phases 8-11)

- [ ] Phase 8: UI dashboard implementation
  - Create sidebar navigation (`Logs`, `Analytics`, `Compare`, `Settings`, `Export`).
  - Redesign `MainWindow.xaml` layout into dashboard panels (log grid, detail pane, analytics widgets).
  - Build analytics dashboard components (charts for error frequency, trend lines, heatmaps).
  - Add bookmark/tag/alert management panels with drag-resizable splitters.
  - Hook commands from `EnhancedMainViewModel` into new UI elements.
  - Update theming resources for dashboard styling (Dark/Light/Solarized/HighContrast).

- [ ] Phase 9: Virtual scrolling & performance optimization
  - Implement data virtualization for the log list (e.g., `DataGrid` virtualization or custom virtualized panel).
  - Introduce background loading pipeline for large log files to keep UI responsive.
  - Add caching layer for frequently accessed log segments.
  - Profile memory/CPU usage with 1M+ log entries and tune accordingly.
  - Surface performance metrics (load time, UI FPS) in developer diagnostics panel.

- [ ] Phase 10: Accessibility enhancements
  - Ensure full keyboard navigation across the dashboard (tab order, accelerators).
  - Add screen reader-friendly `AutomationProperties` to key UI elements.
  - Implement high-contrast theme refinements and verify WCAG AA color ratios.
  - Provide large-text mode toggles and ensure layout scales gracefully.
  - Add focus indicators and skip-navigation shortcuts.

- [ ] Phase 11: Cross-platform packaging & auto-update
  - Evaluate cross-platform UI strategy (WPF + Windows packaging vs MAUI/hybrid for macOS/Linux).
  - Create platform-specific installers (MSIX/MSI, notarized macOS bundle, AppImage/DEB/RPM).
  - Integrate auto-update service (update check, background download, install, rollback support).
  - Host release metadata + artifacts and surface release notes inside the app.
  - Add telemetry hooks for update adoption (opt-in) and error reporting.
