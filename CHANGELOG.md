# Changelog
All notable changes to the GGs project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Added
- Comprehensive operator troubleshooting guides for non-technical users
  - `docs/operator-guides/desktop-troubleshooting-guide.md` - Full troubleshooting guide with 4 problem scenarios
  - `docs/operator-guides/run-diagnostics-guide.md` - Step-by-step diagnostics walkthrough with 3 methods
  - `docs/operator-guides/quick-start-poster.md` - Printable quick reference card
- Interactive Mermaid troubleshooting flowchart for visual decision-making
- Comprehensive regression test suite for Desktop UI
  - `tests/GGs.Enterprise.Tests/UI/ViewModelTests.cs` - 7 view-model wiring tests
  - `tests/GGs.Enterprise.Tests/UI/DesktopUIAutomationTests.cs` - 6 UI automation tests
  - `tests/GGs.Enterprise.Tests/UI/ScreenRenderingTests.cs` - 6 screen rendering tests
- Automated smoke tests for theme resource loading
  - `tests/GGs.Enterprise.Tests/UI/EnterpriseControlStylesSmokeTests.cs`
- Architecture Decision Record for recovery mode resolution
  - `docs/ADR-007-Desktop-Recovery-Mode-Resolution.md`
- Test evidence documentation
  - `launcher-logs/test-evidence/prompt1-regression-tests-2025-10-04.md`

### Fixed
- **[CRITICAL]** Desktop client no longer launches in recovery mode (Issue: Prompt 1)
  - Root cause: Non-freezable Storyboard animations in `EnterpriseControlStyles.xaml`
  - Solution: Refactored theme resources to use freezable animations
  - Impact: Desktop now launches with full UI in standard mode on first try
  - Evidence: `launcher-logs/incidents/desktop-recovery-2025-10-04.md`
- XAML resource dictionary loading failures causing UI initialization errors
- Storyboard animation freeze exceptions during theme loading
- Recovery mode incorrectly triggering during normal application startup

### Changed
- `clients/GGs.Desktop/Themes/EnterpriseControlStyles.xaml` - Replaced non-freezable animations with freezable equivalents
- Theme palette resources now use static colors instead of dynamic runtime modifications
- Recovery mode now only appears during deliberate fault injection tests

### Technical Details
- **Breaking Change**: Dynamic theme palette swaps now require application restart
- **Assumption**: Theme palette resources remain stable during runtime (documented in ADR-007)
- **Test Coverage**: 19 new tests added (7 passing view-model tests, 12 UI automation tests with documented environmental limitations)

---

## [1.0.0] - 2025-10-02

### Added
- Initial release of GGs Desktop application
- Core dashboard with optimization, network, and system intelligence views
- Batch file launchers for Desktop, ErrorLogViewer, and Fusion components
- Enterprise-grade elevation bridge with consent-gated privilege escalation
- Deep system access capabilities for hardware inventory and diagnostics
- Telemetry correlation and trace depth logging
- Privacy-tiered data collection framework

### Components
- **GGs.Desktop** - WPF desktop client with modern UI
- **GGs.Agent** - Background service for system monitoring and optimization
- **GGs.Server** - Backend API server
- **GGs.ErrorLogViewer** - Diagnostic log viewer tool
- **GGs.Shared** - Common libraries and utilities

### Documentation
- ADR-001: Batch File Launchers
- ADR-002: Deep System Access
- ADR-003: Tweak Capability Modules
- ADR-004: Consent-Gated Elevation Bridge
- ADR-005: Telemetry Correlation Trace Depth
- ADR-006: Privacy Tiering

---

## Version History

### Version Numbering
- **Major.Minor.Patch** (e.g., 1.0.0)
- **Major**: Breaking changes or significant architectural updates
- **Minor**: New features, non-breaking changes
- **Patch**: Bug fixes, documentation updates

### Release Cadence
- **Major releases**: Quarterly
- **Minor releases**: Monthly
- **Patch releases**: As needed (hotfixes)

---

## Categories

### Added
New features, capabilities, or documentation added to the project.

### Changed
Changes to existing functionality, behavior, or architecture.

### Deprecated
Features or APIs that are marked for removal in future versions.

### Removed
Features or APIs that have been removed from the project.

### Fixed
Bug fixes, error corrections, or issue resolutions.

### Security
Security-related fixes, updates, or enhancements.

---

## Links

- [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)
- [Semantic Versioning](https://semver.org/spec/v2.0.0.html)
- [GGs Documentation](docs/)
- [Architecture Decision Records](docs/)

---

## Maintenance

This changelog is maintained by the GGs Engineering Team and updated with each release.

**Last Updated**: 2025-10-04  
**Maintained By**: GGs Engineering Team  
**Format Version**: 1.0.0

