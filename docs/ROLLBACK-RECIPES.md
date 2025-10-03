# Rollback Recipes for GGsDeepAgent vNext

**Version:** 1.0.0  
**Last Updated:** 2025-10-03  
**Owner:** GGsDeepAgent Team

---

## Overview

This document provides step-by-step rollback procedures for each deployable unit in GGsDeepAgent vNext. All rollback procedures are **tested, idempotent, and can be executed multiple times safely**.

---

## Quick Reference

| Component | Rollback Time | Risk Level | Recipe |
|-----------|---------------|------------|--------|
| Launchers | 2 minutes | Low | [Rollback Launchers](#rollback-launchers) |
| Desktop App | 5 minutes | Medium | [Rollback Desktop](#rollback-desktop-app) |
| Server | 10 minutes | High | [Rollback Server](#rollback-server) |
| Agent | 10 minutes | High | [Rollback Agent](#rollback-agent) |
| CI/CD Pipeline | 5 minutes | Low | [Rollback CI/CD](#rollback-cicd-pipeline) |
| Build Configuration | 3 minutes | Medium | [Rollback Build Config](#rollback-build-configuration) |

---

## Prerequisites

Before performing any rollback:

1. ✅ **Backup current state:**
   ```batch
   git stash save "Pre-rollback backup %date% %time%"
   ```

2. ✅ **Stop all running processes:**
   ```batch
   taskkill /F /IM GGs.Desktop.exe /T
   taskkill /F /IM GGs.ErrorLogViewer.exe /T
   taskkill /F /IM GGs.Server.exe /T
   taskkill /F /IM GGs.Agent.exe /T
   ```

3. ✅ **Verify Git status:**
   ```batch
   git status
   ```

4. ✅ **Create rollback tag:**
   ```batch
   git tag -a rollback-%date:~-4,4%%date:~-10,2%%date:~-7,2% -m "Rollback point"
   ```

---

## Rollback Launchers

### When to Use
- Launchers are broken or not working
- Users cannot launch applications
- Smoke tests fail

### Risk Level: **Low**
- No data loss
- No service disruption
- Can be rolled back instantly

### Procedure

1. **Identify previous working version:**
   ```batch
   git log --oneline --all -- Launch-*.bat
   ```

2. **Rollback to previous version:**
   ```batch
   git checkout HEAD~1 -- Launch-Desktop.bat
   git checkout HEAD~1 -- Launch-ErrorLogViewer.bat
   git checkout HEAD~1 -- Launch-All.bat
   git checkout HEAD~1 -- Test-Launchers.bat
   ```

3. **Verify rollback:**
   ```batch
   Test-Launchers.bat
   ```
   Expected: All 7 tests pass

4. **Test launchers:**
   ```batch
   Launch-Desktop.bat
   ```
   Expected: Desktop app launches successfully

5. **Commit rollback:**
   ```batch
   git add Launch-*.bat Test-Launchers.bat
   git commit -m "Rollback launchers to previous working version"
   ```

### Validation
- ✅ All smoke tests pass
- ✅ Desktop app launches
- ✅ ErrorLogViewer launches
- ✅ No errors in logs

### Rollback Time: **2 minutes**

---

## Rollback Desktop App

### When to Use
- Desktop app crashes on startup
- Critical bugs in production
- Performance degradation

### Risk Level: **Medium**
- No data loss (if database schema unchanged)
- Service disruption for desktop users
- May require database rollback

### Procedure

1. **Stop Desktop app:**
   ```batch
   taskkill /F /IM GGs.Desktop.exe /T
   ```

2. **Identify previous working version:**
   ```batch
   git log --oneline --all -- GGs/clients/GGs.Desktop/
   ```

3. **Rollback source code:**
   ```batch
   git checkout HEAD~1 -- GGs/clients/GGs.Desktop/
   ```

4. **Clean and rebuild:**
   ```batch
   dotnet clean GGs/clients/GGs.Desktop/GGs.Desktop.csproj
   dotnet build GGs/clients/GGs.Desktop/GGs.Desktop.csproj -c Release
   ```

5. **Run tests:**
   ```batch
   dotnet test GGs/tests/GGs.Enterprise.Tests/GGs.Enterprise.Tests.csproj
   ```

6. **Launch and verify:**
   ```batch
   Launch-Desktop.bat
   ```

7. **Commit rollback:**
   ```batch
   git add GGs/clients/GGs.Desktop/
   git commit -m "Rollback Desktop app to previous working version"
   ```

### Validation
- ✅ App launches without errors
- ✅ All tests pass
- ✅ No crashes in first 5 minutes
- ✅ Logs show no errors

### Rollback Time: **5 minutes**

---

## Rollback Server

### When to Use
- Server crashes or won't start
- API breaking changes
- Database connection issues
- Critical security vulnerability

### Risk Level: **High**
- Potential data loss if database schema changed
- Service disruption for all users
- May require database rollback

### Procedure

1. **Stop Server:**
   ```batch
   taskkill /F /IM GGs.Server.exe /T
   ```

2. **Backup database (if schema changed):**
   ```batch
   :: Backup command depends on database type
   :: For SQL Server:
   sqlcmd -S localhost -Q "BACKUP DATABASE GGsDeepAgent TO DISK='C:\Backups\GGsDeepAgent_rollback.bak'"
   ```

3. **Identify previous working version:**
   ```batch
   git log --oneline --all -- GGs/server/GGs.Server/
   ```

4. **Rollback source code:**
   ```batch
   git checkout HEAD~1 -- GGs/server/GGs.Server/
   ```

5. **Rollback database migrations (if needed):**
   ```batch
   dotnet ef database update <PreviousMigration> --project GGs/server/GGs.Server/GGs.Server.csproj
   ```

6. **Clean and rebuild:**
   ```batch
   dotnet clean GGs/server/GGs.Server/GGs.Server.csproj
   dotnet build GGs/server/GGs.Server/GGs.Server.csproj -c Release
   ```

7. **Run tests:**
   ```batch
   dotnet test GGs/tests/GGs.Enterprise.Tests/GGs.Enterprise.Tests.csproj
   ```

8. **Launch and verify:**
   ```batch
   cd GGs/server/GGs.Server/bin/Release/net9.0
   start GGs.Server.exe
   ```

9. **Verify health endpoint:**
   ```batch
   curl http://localhost:5000/health
   ```
   Expected: `{"status":"healthy"}`

10. **Commit rollback:**
    ```batch
    git add GGs/server/GGs.Server/
    git commit -m "Rollback Server to previous working version"
    ```

### Validation
- ✅ Server starts without errors
- ✅ Health endpoint returns 200 OK
- ✅ All tests pass
- ✅ API endpoints respond correctly
- ✅ Database queries work

### Rollback Time: **10 minutes**

---

## Rollback Agent

### When to Use
- Agent crashes or won't start
- High CPU/memory usage
- Monitoring not working

### Risk Level: **High**
- Service disruption for monitoring
- May lose telemetry data
- System tweaks may not apply

### Procedure

1. **Stop Agent:**
   ```batch
   taskkill /F /IM GGs.Agent.exe /T
   ```

2. **Identify previous working version:**
   ```batch
   git log --oneline --all -- GGs/agent/GGs.Agent/
   ```

3. **Rollback source code:**
   ```batch
   git checkout HEAD~1 -- GGs/agent/GGs.Agent/
   ```

4. **Clean and rebuild:**
   ```batch
   dotnet clean GGs/agent/GGs.Agent/GGs.Agent.csproj
   dotnet build GGs/agent/GGs.Agent/GGs.Agent.csproj -c Release
   ```

5. **Run tests:**
   ```batch
   dotnet test GGs/tests/GGs.Enterprise.Tests/GGs.Enterprise.Tests.csproj
   ```

6. **Launch and verify:**
   ```batch
   cd GGs/agent/GGs.Agent/bin/Release/net9.0-windows
   start GGs.Agent.exe
   ```

7. **Verify agent is running:**
   ```batch
   tasklist | findstr GGs.Agent.exe
   ```

8. **Commit rollback:**
   ```batch
   git add GGs/agent/GGs.Agent/
   git commit -m "Rollback Agent to previous working version"
   ```

### Validation
- ✅ Agent starts without errors
- ✅ Process is running
- ✅ All tests pass
- ✅ Telemetry is being collected
- ✅ Logs show no errors

### Rollback Time: **10 minutes**

---

## Rollback CI/CD Pipeline

### When to Use
- CI/CD pipeline failing
- Build/test/deploy issues
- GitHub Actions errors

### Risk Level: **Low**
- No service disruption
- No data loss
- Only affects future deployments

### Procedure

1. **Identify previous working version:**
   ```batch
   git log --oneline --all -- .github/workflows/ci.yml
   ```

2. **Rollback workflow file:**
   ```batch
   git checkout HEAD~1 -- .github/workflows/ci.yml
   ```

3. **Commit rollback:**
   ```batch
   git add .github/workflows/ci.yml
   git commit -m "Rollback CI/CD pipeline to previous working version"
   ```

4. **Push to GitHub:**
   ```batch
   git push origin main
   ```

5. **Verify workflow:**
   - Go to GitHub Actions tab
   - Check that workflow runs successfully

### Validation
- ✅ Workflow runs without errors
- ✅ All jobs pass (build, test, package, health, security)
- ✅ Artifacts are generated

### Rollback Time: **5 minutes**

---

## Rollback Build Configuration

### When to Use
- Build warnings/errors
- Analyzer issues
- Version conflicts

### Risk Level: **Medium**
- May affect all projects
- May require full rebuild
- May affect CI/CD

### Procedure

1. **Identify previous working version:**
   ```batch
   git log --oneline --all -- GGs/Directory.Build.props
   ```

2. **Rollback build configuration:**
   ```batch
   git checkout HEAD~1 -- GGs/Directory.Build.props
   ```

3. **Clean all projects:**
   ```batch
   dotnet clean GGs/GGs.sln
   rmdir /s /q GGs\bin GGs\obj
   ```

4. **Rebuild solution:**
   ```batch
   dotnet build GGs/GGs.sln -c Release
   ```

5. **Run tests:**
   ```batch
   dotnet test GGs/GGs.sln -c Release
   ```

6. **Commit rollback:**
   ```batch
   git add GGs/Directory.Build.props
   git commit -m "Rollback build configuration to previous working version"
   ```

### Validation
- ✅ Solution builds without errors
- ✅ Zero warnings
- ✅ All tests pass
- ✅ Executables are generated

### Rollback Time: **3 minutes**

---

## Emergency Rollback (Nuclear Option)

### When to Use
- Multiple components broken
- Unknown root cause
- Critical production issue

### Risk Level: **Critical**
- May lose recent changes
- Service disruption
- Requires full system restart

### Procedure

1. **Stop all processes:**
   ```batch
   taskkill /F /IM GGs.*.exe /T
   ```

2. **Rollback to last known good commit:**
   ```batch
   git log --oneline --all
   :: Find last known good commit (e.g., abc1234)
   git reset --hard abc1234
   ```

3. **Clean everything:**
   ```batch
   dotnet clean GGs/GGs.sln
   rmdir /s /q GGs\bin GGs\obj
   ```

4. **Rebuild everything:**
   ```batch
   dotnet build GGs/GGs.sln -c Release
   ```

5. **Run all tests:**
   ```batch
   dotnet test GGs/GGs.sln -c Release
   ```

6. **Run smoke tests:**
   ```batch
   Test-Launchers.bat
   ```

7. **Launch all apps:**
   ```batch
   Launch-All.bat
   ```

### Validation
- ✅ All smoke tests pass
- ✅ All unit tests pass
- ✅ All apps launch successfully
- ✅ No errors in logs

### Rollback Time: **15 minutes**

---

## Artifact Retention

All artifacts are retained for rollback purposes:

| Artifact | Retention | Location |
|----------|-----------|----------|
| Git commits | Forever | GitHub repository |
| Build artifacts | 30 days | `GGs/*/bin/Release/` |
| MSI installers | 90 days | GitHub Actions artifacts |
| Logs | 30 days | `launcher-logs/` |
| Database backups | 90 days | `C:\Backups\` |

---

## References

- [ADR-001: Batch File Launchers](ADR-001-Batch-File-Launchers.md)
- [RUNBOOK: Launcher Troubleshooting](RUNBOOK-Launcher-Troubleshooting.md)
- [EliNextSteps - Phase 8: Handoff & Rollback](../EliNextSteps)

---

**Last Reviewed:** 2025-10-03  
**Next Review:** 2025-11-03  
**Owner:** GGsDeepAgent Team

