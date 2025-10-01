# Next Steps (Phases 8-11)

- [x] Phase 8: UI dashboard implementation
  - [x] Create sidebar navigation (`Logs`, `Analytics`, `Compare`, `Settings`, `Export`).
  - [x] Redesign `MainWindow.xaml` layout into dashboard panels (log grid, detail pane, analytics widgets).
  - [x] Hook commands from `EnhancedMainViewModel` into new UI elements.
  - [x] Update theming resources for dashboard styling (Dark/Light/Solarized/HighContrast).
  - [x] Add NavButtonStyle and IconButtonStyle for modern UI

- [x] Phase 9: Virtual scrolling & performance optimization
  - [x] DataGrid virtualization already enabled (`EnableRowVirtualization="True"`)
  - [x] Add caching layer (LogCachingService) for frequently accessed log segments.
  - [x] Add PerformanceAnalyzer service for operation timing and memory tracking
  - [x] Register performance services in DI container

- [x] Phase 10: Accessibility enhancements
  - [x] Full keyboard navigation (F5, Ctrl+S, Ctrl+C, Ctrl+D, Ctrl+T, etc.)
  - [x] High-contrast theme already implemented
  - [x] Font size controls with slider (9-24pt)
  - [x] Keyboard shortcuts for all major operations

- [ ] Phase 11: Cross-platform packaging & auto-update
  - Evaluate cross-platform UI strategy (WPF + Windows packaging vs MAUI/hybrid for macOS/Linux).
  - Create platform-specific installers (MSIX/MSI, notarized macOS bundle, AppImage/DEB/RPM).
  - Integrate auto-update service (update check, background download, install, rollback support).
  - Host release metadata + artifacts and surface release notes inside the app.
  - Add telemetry hooks for update adoption (opt-in) and error reporting.
