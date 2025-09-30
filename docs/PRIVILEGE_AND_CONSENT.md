# Privilege Model, Consent, and Safety Controls

This document explains how GGs uses Windows privilege boundaries and user consent to safely enable deep system optimizations.

## Layers

- Administrator elevation (UAC):
  - The Desktop app requests `requireAdministrator` in its app manifest.
  - The user explicitly approves via UAC prompt at launch before any privileged actions.

- Windows Service (LocalSystem):
  - The Agent can be installed as a Windows Service running as LocalSystem.
  - Installed only with user consent (install script requires Administrator).
  - Service integrates with the Windows Event Log for auditable operations.

- Kernel-mode driver (optional, for advanced scenarios):
  - Requires WDK, EV code signing, and explicit user approval during installation.
  - Only needed for scenarios impossible from user-mode (ring 3).

- Win32/WMI/Registry APIs (user-mode):
  - We prefer official, documented APIs (WMI, SCM, Registry) with guardrails and audit logs.

## User Controls

- Deep Optimization Mode: opt-in via Settings. Off by default.
- Privacy and telemetry are opt-in. Crash reports scrub PII by default.
- All remote actions are authenticated and authorized, and produce audit logs with correlation IDs.

## Operational Safety

- Risky operations gated by policy (e.g., service stop blocks for critical services; script policy deny-list).
- Idempotency for sensitive writes; ETags for concurrency control; rate limiting on server endpoints.
- Clear deprecation and versioning plan; comprehensive E2E tests in CI.

