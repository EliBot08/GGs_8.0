# ANALYSIS-DRIVEN BUILD PROMPTS (Run 3/3 — Production Hardening and Go‑Live)

Purpose
- Finalize enterprise readiness by closing any remaining backend↔frontend gaps, enforcing strong security defaults, and adding SRE-grade operability. Each prompt has concrete files, contracts, tests, and rollout notes. One feature per PR.

Conventions
- Server: server/GGs.Server
- Desktop (WPF): clients/GGs.Desktop
- Agent: agent/GGs.Agent
- Shared: shared/GGs.Shared
- Web admin (React): src (services under src/services, components under src/components)
- Tests: tests/GGs.E2ETests

New/confirmed gaps in this run
- Agent posts audit to /api/auditlogs (legacy) rather than /api/audit/log with machine token/mTLS; no ACK over hub; no heartbeats.
- Device registrations are in-memory (DeviceIdentityService) — persistence, search, paging, and presence are still missing.
- Web admin services still absent (analytics/user/role/license/http/auth) — pages reference them but cannot call the backend.
- Analytics endpoints unused in Desktop; Cloud Profiles lack admin UI; advanced Audit search not exposed in Desktop.

PROMPT 01 — Fail-fast production configuration checks [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: Prevent boot with placeholder secrets or unsafe defaults.
- Backend (Program.cs):
  - If app.Environment.IsProduction():
    - Require non-placeholder Auth:JwtKey; if missing -> fail startup with clear message.
    - Require License:PublicKeyPem set; log error and fail if absent.
    - Disallow Swagger UI unless Server:AllowSwaggerInProd=true.
    - Disallow AllowAnyOrigin; require configured CORS origins.
  - Add /live (liveness) that always returns 200; update /ready to assert DB reachable and pending migrations == 0; return 503 otherwise.
- Tests: Start in Development (passes), start in a simulated Production with missing secrets (fails), /live and /ready behave as specified.

PROMPT 02 — Device registrations: durable store + admin paging [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: In-memory registration cannot be managed at scale or after restarts.
- Backend:
  - server/GGs.Server/Models/DeviceRegistration.cs (Id PK, DeviceId UNIQUE, Thumbprint, CommonName, RegisteredUtc, LastSeenUtc, RevokedUtc, IsActive bool).
  - AppDbContext.cs: DbSet<DeviceRegistration> DeviceRegistrations.
  - Refactor Services/DeviceIdentityService.cs to use AppDbContext CRUD; methods are async and update LastSeenUtc.
  - EF migration AddDeviceRegistrations with indexes: IX_DeviceRegistrations_DeviceId (unique), IX_DeviceRegistrations_IsActive, IX_DeviceRegistrations_LastSeenUtc.
- Controllers:
  - DevicesController.Get: add paging (skip/take), total count header (X-Total-Count), and filters (isActive, q=DeviceId contains).
- Desktop: DeviceManagementViewModel to use paged API and display totals; add search by deviceId.
- Tests: E2E enroll -> present in DB -> revoke -> IsActive=false; paging returns correct totals.

PROMPT 03 — Device online presence and heartbeats (server + agent) [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: Ops needs to know what’s online right now.
- Backend:
  - Hubs/AdminHub.cs: add public Task Heartbeat(string deviceId). On call, update DeviceRegistration.LastSeenUtc if device registered; add in-memory DeviceRegistry as now.
  - Add /api/devices/online returning DeviceId[] currently connected; add /api/devices/summary returning registered vs. online counts.
  - Hosted background service expiring stale online connections if no heartbeat for 120s.
- Agent (Worker.cs): call Heartbeat(deviceId) every 30s via hub; log warnings on failures; keep mTLS-enabled HTTP client for fallback audit.
- Tests: Simulate online/offline transitions; /api/devices/online reflects status.

PROMPT 04 — Unify audit ingestion via secure path + legacy fallback [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: Standardize ingestion path with auth guarantees.
- Backend:
  - AuditController.Log already supports machine token/mTLS. Document header X-Machine-Token.
  - Mark AuditLogsController (legacy) as deprecated; limit to Admin-only soon.
- Agent (Worker.cs):
  - Prefer POST /api/audit/log with header X-Machine-Token from env AGENT_MACHINE_TOKEN when provided; fallback to /api/auditlogs if 401/404.
  - Include X-Correlation-ID on all HTTP posts.
- Tests: With machine token set -> 200; without token and no mTLS -> 401; fallback path posts to legacy controller.

PROMPT 05 — Remote execution ACK over hub + correlated audit [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: Complete the control loop with real-time acknowledgment.
- Backend:
  - AdminHub: add ReportExecutionResult(TweakApplicationLog log). Persist to DB and broadcast to admins group "audit:added" with correlationId.
  - RemoteController.Execute: include correlationId in response and set in hub message context.
- Agent:
  - After TweakExecutor.Apply, call hub ReportExecutionResult(log) in addition to HTTP fallback.
- Desktop:
  - RemoteManagementViewModel: display "Delivered" -> "Executed" status transitions using correlationId, with NotificationCenter entry on completion.
- Tests: E2E correlates execution to stored log; desktop toast appears.

PROMPT 06 — Desktop Analytics: server-first with graceful fallback [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: Use authoritative metrics, reduce client compute.
- Desktop:
  - ApiClient: add GetAnalyticsSummaryAsync(days), GetTopTweaksAsync(days, top), GetAnalyticsDevicesAsync().
  - AnalyticsViewModel: when Entitlements allow, call server; on 403/404, show banner and fallback to local computation.
- Tests: Admin path -> server data; Support/Basic -> fallback path.

PROMPT 07 — Web foundational services (http/auth) and analyticsService [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: Enable web admin to consume APIs.
- Web:
  - src/services/http.ts: base URL from window.env.API_BASE_URL || process.env, X-Correlation-ID, bearer injection, refresh-on-401, ProblemDetails mapping.
  - src/services/authService.ts: login, refresh; memory + sessionStorage; rotate before expiry.
  - src/services/analyticsService.ts: getSummary(days), getTopTweaks(days, top), getDevices().
- Tests: Simulated with .NET E2E: Auto-refresh-on-401 behavior verified via DelegatingHandler; analytics summary smoke tested.

PROMPT 08 — RolesController + Web roleService [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: Web admins need roles overview.
- Backend: server/GGs.Server/Controllers/RolesController.cs [Authorize(Roles="Admin")] GET /api/roles -> [{ name, userCount }].
- Web: src/services/roleService.ts getAllRoles().
- Tests: E2E admin fetch shows expected roles (implemented in .NET E2E tests). Unauthenticated request returns 401.

PROMPT 09 — Users: role changes, CSV import, welcome emails [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: Enterprise onboarding.
- Backend:
  - UsersController: POST /api/users/{id}/roles body { roles: string[] } -> set roles transactionally.
  - POST /api/users/import (IFormFile CSV; Email,Password,Roles; 1MB; content-type check) -> created list + per-row errors.
  - POST /api/users/{id}/welcome-email via IEmailSender (SMTP config) -> returns 202; dev logs only.
  - ApplicationUser: add MetadataJson string?; migration; GET includes metadata.
- Web: src/services/userService.ts (getAll, create, delete, suspend, unsuspend, changeRoles, importUsers, sendWelcomeEmail) and update UserManagement.tsx to use them.
- Tests: Import CSV sample; change roles; send email 202 in dev.

PROMPT 10 — License lifecycle: fields, transitions, enforcement [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: Web UI expects rich license states; enforce device max and usage.
- Backend (Models/LicenseRecord): add Status (Active|Suspended|Revoked), DeveloperMode bool, MaxDevices int, UsageCount int, AssignedDevices (JSON array). Migration.
- LicensesController:
  - POST /api/licenses/suspend/{id}, /activate/{id}, and /update/{id} body { MaxDevices?, DeveloperMode?, Notes? }.
  - On /validate: increment UsageCount; if DeviceBindingId present and not in AssignedDevices -> append (dedup, cap 10). Enforce MaxDevices >= assigned count.
- Web: licenseService.ts implements getAll/create/revoke/suspend/activate/update mapping to UI.
- Tests: E2E lifecycle and enforcement errors.

PROMPT 11 — License issuance: idempotency + abuse controls [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: Safe retries; guard mass issuance.
- Backend:
  - IdempotencyMiddleware for POST /api/licenses/issue (store key, request hash, response).
  - Rate limit license issuance per admin via AspNet rate limiter.
- Tests: Replay with same key -> single license; exceeding rate -> 429.

PROMPT 12 — JWT/refresh hardening + JWKS [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: Token hygiene at scale.
- Backend:
  - AuthController: include jti in access tokens; accept configurable token lifetimes; deny refresh after absolute lifetime; store short-lived revoked jti cache.
  - Add JwksController returning current public keys; permit rotation (kid).
- Desktop/Web: no change.
- Tests: JWKS reachable; revoked jti blocked; refresh beyond max age -> 401.

PROMPT 13 — API versioning and deprecation headers [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: Manage evolution.
- Backend:
  - Added v1 routes under /api/v1 across controllers while keeping legacy /api/*.
  - Middleware emits Deprecation: true and Sunset headers for legacy routes; Link header points to successor version.
  - Swagger configured with v1 doc grouping.
- Clients: Prefer v1 URLs; fallback allowed temporarily.
- Tests: New E2E verifies /api/v1 is functional and legacy emits headers.

PROMPT 14 — ProblemDetails and FluentValidation everywhere [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: Uniform errors; robust input validation.
- Backend:
  - Integrated FluentValidation with auto-validation; validators added for CreateTweakRequest, UpdateTweakRequest, LicenseIssueRequest, Auth Login/Refresh, DeviceEnrollRequest.
  - Controllers rely on automatic 400 ProblemDetails for invalid payloads.
- Tests: New E2E asserts invalid payloads return 400 with ProblemDetails content type.

PROMPT 15 — Tweak concurrency with ETag (If-Match) [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: Prevent last-write-wins.
- Backend: TweaksController GET adds ETag header (from UpdatedUtc/CreatedUtc-based hash); PUT requires If-Match; missing -> 428, mismatch -> 412; If-None-Match -> 304.
- Desktop: Include If-Match when saving edits.
- Tests: Added E2E verifying success with correct ETag, 412 on stale, 428 when missing; requires clean test DB.

PROMPT 16 — Users/Tweaks pagination, filtering, sorting [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: Performance and UX.
- Backend: UsersController and TweaksController now accept page/pageSize/q/sort/desc; capped pageSize (<=100); return X-Total-Count header.
- Clients: Implement pageable lists.
- Tests: Added E2E to verify totals and ordering; requires clean test DB.

PROMPT 17 — Tighten CORS + security headers [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: Production posture.
- Backend: Configured AllowedOrigins; added HSTS in production; added X-Content-Type-Options, X-Frame-Options, and basic CSP headers; /live and /ready marked AllowAnonymous.
- Tests: E2E checks headers (dev variant) and liveness; production variant requires configuring License:PublicKeyPem.

PROMPT 18 — OpenTelemetry traces + resource attributes [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: Deep observability.
- Backend: Added optional OTEL tracing for ASP.NET, EF Core, HttpClient; resource attributes service.name=GGs.Server, deployment.environment; OTLP exporter (gated via Otel:ServerEnabled).
- Agent/Desktop: existing ActivitySources are fine; ensure X-Correlation-ID flows via header into tracing context.
- Tests: Smoke tests included to verify server starts with OTEL enabled/disabled; requires clean test DB.

PROMPT 19 — Structured JSON logging with Serilog [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: Better query/alerts.
- Backend: Integrated Serilog; JSON console and rolling file sinks; request logging middleware enabled; correlation id scope captured.
- Tests: Smoke test ensures request logging does not block requests; requires clean test DB.

PROMPT 20 — Analytics enrichment endpoints [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: Dashboards need breakdowns.
- Backend (AnalyticsController): add
  - GET /api/analytics/licenses-by-tier -> [{ tier, count, activeCount }]
  - GET /api/analytics/tweaks-failures-top?days=&top=
  - GET /api/analytics/active-devices?minutes=
- Clients: Add panels to Desktop/Web.
- Tests: Seed and validate outputs.

PROMPT 21 — Audit search UI for Desktop [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: Utilize server search.
- Desktop: Views/Analytics/AuditSearchView.xaml (+ VM) with filters for deviceId, userId, tweakId, from, to, success; use ApiClient.SearchAuditAsync; grid with paging.
- Tests: Filter combinations return correct subsets.

PROMPT 22 — Cloud Profiles: Desktop marketplace UI with signature trust [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: Safe content.
- Desktop: Views/Admin/CloudProfilesView.xaml (+ VM) to list/search/download; show signature fingerprint and issuer; CloudProfileService verifies signature against configured trusted fingerprints; warn otherwise.
- Tests: Trusted signature -> OK; unknown -> warning.

PROMPT 23 — Cloud Profiles: Admin moderation [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: Control exposure.
- Backend: POST /api/profiles/{id}/approve sets ModerationApproved=true (Admin only); GET list/search hides unapproved for non-admins.
- Clients: Admin can approve; basic users only see approved.
- Tests: Approval toggles visibility.

PROMPT 24 — mTLS for Audit and Ingest (configurable) [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: Secure ingestion paths.
- Backend: If Security:ClientCertificate:Enabled=true, require client certificate for POST /api/audit/log and /api/ingest/events; otherwise allow machine token.
- Agent/Desktop: Environment enables client cert via DeviceEnrollmentService.
- Tests: 401 when cert absent; 200 with valid cert.

PROMPT 25 — IngestController durability + backpressure [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: Don’t lose telemetry; avoid overload.
- Backend: Persist events in table IngestEvents (EventId unique, Type, Payload JSON, CreatedUtc, Client, ProcessedUtc); per-IP rate limit and max body size; return accepted count.
- Agent/Desktop: No change beyond existing OfflineQueue.
- Tests: POST persists; 413 on oversized body; 429 on flood.

PROMPT 26 — Crash reporting: Desktop→OfflineQueue→Ingest [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: Reliable crash telemetry.
- Desktop: CrashReportingService.CaptureException enqueues anonymized crash.report to OfflineQueueService; queue posts to /api/ingest/events.
- Tests: Simulated exception produces queued row and dispatch log.

PROMPT 27 — Secrets: key vault/env integration [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: Eliminate plaintext secrets.
- Backend: Add configuration provider to pull secrets (JWT keys, license private key, SMTP creds) from env/KeyVault; prohibit plaintext in appsettings.production.json.
- Tests: With missing secrets -> fail fast; with env present -> startup OK.

PROMPT 28 — JWT key rotation + JWKS kid selection [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: Continuous crypto hygiene.
- Backend: Support multiple signing keys with kid; rotate on schedule; JWKS lists active public keys; tokens include kid header.
- Tests: Old kid accepted until sunset; new tokens use latest kid.

PROMPT 29 — RBAC -> policy-based ABAC preparation [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: Finer control.
- Backend: Add permission policies (ManageUsers, ManageLicenses, ExecuteRemote, ViewAnalytics). Map roles to policies in config. Decorate endpoints accordingly; 403 if lacking.
- Desktop/Web: Prepare to honor permissions claim when available (keep role fallback).
- Tests: Access denied for improper policy.

PROMPT 30 — Tweak execution safety policy in agent [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: Reduce blast radius.
- Agent: ScriptPolicy already blocks risky commands; add environment variable GGS_SCRIPTS_MODE=strict|moderate|permissive (default moderate). In strict mode, allowlist prefixes as implemented.
- Backend/Desktop: Provide UI toggle (Admin only) to view current mode; documentation.
- Tests: Blocked script returns error with policy message.

PROMPT 31 — Response caching + ETags for static data [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: Performance.
- Backend: Enable ResponseCaching middleware; add [ResponseCache] attributes where safe (e.g., GET profiles/{id}, analytics summary for small TTL); add ETag headers for GET /api/tweaks/{id}.
- Tests: Repeated GETs served with 304 when ETag matches.

PROMPT 32 — Kestrel and request limits [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: Stability under load.
- Backend: Configure Kestrel limits (MaxRequestBodySize ~2 MB by default; RequestHeadersTimeout; KeepAliveTimeout). Configure max concurrent connections based on env.
- Tests: Oversized request -> 413; idle keepalive closes.

PROMPT 33 — Database readiness and migration gate [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: Avoid schema drift.
- Backend: On startup, in Production: if pending migrations -> fail with clear message. Provide admin endpoint /admin/migrations (Admin only) to view migration status.
- Tests: Pending migrations cause startup failure in prod simulation; dev continues.

PROMPT 34 — Data retention and archival for audit logs [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: Control growth and compliance.
- Backend: Add background job to archive TweakLogs older than 90 days to a secondary table or file (with compression); configurable retention.
- Tests: After job runs, old rows moved; totals match.

PROMPT 35 — Desktop/Web entitlements binding [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: Clean UI gating.
- Desktop: Bind buttons (edit/delete/execute) to EntitlementsService.HasCapability; update on AuthService.RolesChanged.
- Web: Gate admin actions in UI based on roles; later read permissions claim if provided.
- Tests: Support user cannot execute administrative actions.

PROMPT 36 — Blue/green deployment guidance + health gates [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: Safer releases.
- Docs/CI: Update .github/workflows/ci.yml to expose environment parameter; publish artifacts with version; add health check gate post-deploy (calls /ready until OK); rollback step on failures.
- Tests: Pipeline simulates deploy, waits for /ready.

PROMPT 37 — Enhanced Swagger governance [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: Client generation and discoverability.
- Backend: Secure Swagger UI with auth; organize tags (Auth, Users, Licenses, Tweaks, Devices, Analytics, Audit, Profiles); add schema examples; add version selector.
- Tests: Manual review in dev.

PROMPT 38 — Correlation ID propagation end-to-end [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: Traceability.
- Backend: Existing middleware sets X-Correlation-ID; ensure logs include it; add to responses; Hub methods include it in state; ApiClient/Agent attach header on all requests.
- Tests: An end-to-end action shares correlation id in logs across client/server.

PROMPT 39 — Desktop/Agent HTTP resilience policies [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: Robust networks.
- Desktop/Agent: SecureHttpClientFactory already used; confirm retry/backoff, timeout, and client cert PIN settings; add jitter; ensure DNS pinning values configurable via env.
- Tests: Simulate transient failures; retries succeed.

PROMPT 40 — Final E2E suite expansion and CI gates [DONE]
- if anything is unclear make proper assesments and continue
- dont mark this prompt as finished until its been tested and proved to be production ready at enterprise level
- Why: Enforce quality before go-live.
- tests/GGs.E2ETests: add comprehensive tests for roles, licenses (issue/suspend/activate/revoke), users (create/suspend/unsuspend/delete/import), devices (enroll/list/revoke/rotate/online), analytics (summary/failures/licenses-by-tier), profiles (browse/approve/download), and audit search.
- CI: Fail build on E2E failure; publish TRX/JUnit reports.

Go‑Live checklist after this run
- Deprecated endpoints restricted or removed; CORS pinned; secrets externalized; observability hooked; CI green; DB migrations applied; SRE runbooks updated; tokens rotated; devices reporting heartbeats; admin UX fully wired.

Notes
- Keep legacy /api/auditlogs and non-versioned routes for one more minor release; emit deprecation headers and plan removal.
- If production DB will not be Sqlite, add a separate prompt to move to SQL Server or PostgreSQL provider with new connection string and EF provider package.

