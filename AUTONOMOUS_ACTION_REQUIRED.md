# Autonomous Action Required

This document lists items that require manual intervention or decisions beyond automated fixes.

## 1. E2ETests - ASP.NET Core 9 Breaking Changes ⚠️

**Status:** BLOCKED - Requires ~30+ hours of manual migration work  
**Priority:** HIGH  
**Impact:** ~60 test files with 70+ compilation errors

### Issue Details
The main `GGs.E2ETests` project has extensive compilation failures due to ASP.NET Core 9 API breaking changes:

- `UseEnvironment()` moved from `IWebHostBuilder` to `IHostBuilder`
- Missing using statements for `System.Net.Http`, `Microsoft.Extensions.Configuration`, `Microsoft.Extensions.Hosting`
- Service interface changes (OfflineQueueService, DeviceIdentityService constructors)
- Removed/renamed methods (AppLogger.ResetForTesting, AuthService, ApiClient)
- Test framework compatibility issues with WebApplicationFactory

### Files Affected
- ~60 test files in `tests/GGs.E2ETests/` (Prompt*Tests.cs, *ServiceTests.cs, *ControllerTests.cs)
- Multiple service and controller implementations

### Estimated Effort
- **Time:** 20-40 hours of focused migration work
- **Complexity:** High - requires deep understanding of ASP.NET Core 9 changes
- **Risk:** Medium - tests may reveal new application bugs during migration

### Recommended Actions
1. **Short-term:** Document as technical debt, skip E2ETests in CI temporarily
2. **Medium-term:** Create migration guide for ASP.NET Core 9 test patterns
3. **Long-term:** Gradually migrate tests using updated WebApplicationFactory patterns

### Resources Needed
- ASP.NET Core 9 migration documentation
- Updated test patterns for Identity/Auth in .NET 9
- Developer with ASP.NET Core testing expertise

---

## 2. LoadTests - NBomber v6 Breaking Changes ⚠️

**Status:** BLOCKED - API breaking changes in load testing library  
**Priority:** MEDIUM  
**Impact:** 27 compilation errors in load test scenarios

### Issue Details
The `GGs.LoadTests` project fails to compile due to NBomber library upgrade from v5.8 to v6.0:

- `Simulation.InjectPerSec()` API changed
- `ReportFormat` enum moved or renamed
- Scenario API changes (ScenarioName property, method signatures)
- FileStream.Create() signature changes

### Files Affected
- `tests/GGs.LoadTests/LoadTestScenarios.cs`

### Estimated Effort
- **Time:** 4-8 hours
- **Complexity:** Medium - requires NBomber v6 migration guide
- **Risk:** Low - isolated to load tests

### Recommended Actions
1. Review NBomber v6 migration guide: https://nbomber.com/docs/migration-guide
2. Update test scenarios to use new API patterns
3. Consider reverting to NBomber v5.8 if v6 migration is not critical

---

## 3. NewE2ETests - Test Server Configuration Issues ⚠️

**Status:** NEEDS INVESTIGATION  
**Priority:** MEDIUM  
**Impact:** 7 of 9 tests failing due to dependency injection issues

### Issue Details
Tests fail with: `Unable to resolve service for type 'Microsoft.AspNetCore.Identity.UserManager'`

This indicates the test WebApplicationFactory is not properly configured with required Identity services.

### Estimated Effort
- **Time:** 2-4 hours
- **Complexity:** Low-Medium
- **Risk:** Low

### Recommended Actions
1. Add proper Identity service configuration to test factory
2. Ensure test database is seeded with required data
3. Review WebApplicationFactory setup in test files

---

## 4. Test Coverage Baseline - Cannot Establish Yet 📊

**Status:** BLOCKED - Waiting for test compilation fixes  
**Priority:** HIGH  
**Impact:** Cannot measure progress toward 85% coverage goal

### Current Status
- **Passing Tests:** 7 (Enterprise.Tests only)
- **Failing Tests:** 7 (NewE2ETests - DI configuration)
- **Cannot Compile:** ~90 tests (E2ETests, LoadTests)

### Target
- 85% code coverage or +30 percentage points improvement

### Recommended Actions
1. Fix NewE2ETests DI configuration (quick win)
2. Get subset of E2ETests compiling to establish partial baseline
3. Use coverage tools (coverlet) once tests are runnable

---

## 5. No Secrets/Credentials Required ✅

**Status:** COMPLETE  
All configuration uses local development defaults. No external API keys or secrets needed for current work.

---

## Summary

**Immediate Wins Available:**
- ✅ Framework upgrades complete (net9.0)
- ✅ Security vulnerabilities fixed (System.Text.Json)
- ✅ Enterprise.Tests passing (7/7)
- ⚠️ NewE2ETests fixable with DI config (2-4 hours)

**Major Blockers:**
- ⚠️ E2ETests migration (20-40 hours) - **Recommend deferring to dedicated sprint**
- ⚠️ LoadTests migration (4-8 hours) - **Can defer or revert NBomber version**

**Recommendation:**
Continue with other improvement tasks (UI/UX, accessibility, runtime logging, documentation) while E2ETests migration is scheduled as dedicated work.
