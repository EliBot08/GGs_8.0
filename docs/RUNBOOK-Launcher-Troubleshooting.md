# Runbook: Launcher Troubleshooting

**Version:** 1.0.0  
**Last Updated:** 2025-10-03  
**Owner:** GGsDeepAgent Team  
**Severity:** P2 (High)

---

## Overview

This runbook provides step-by-step troubleshooting procedures for GGsDeepAgent launcher issues.

---

## Quick Reference

| Issue | Severity | MTTR | Runbook Section |
|-------|----------|------|-----------------|
| Launcher won't start | P1 | 5 min | [Launcher Won't Start](#launcher-wont-start) |
| Build fails | P2 | 10 min | [Build Failures](#build-failures) |
| App won't launch | P2 | 10 min | [App Launch Failures](#app-launch-failures) |
| Tests fail | P3 | 15 min | [Test Failures](#test-failures) |
| Slow performance | P3 | 20 min | [Performance Issues](#performance-issues) |

---

## Prerequisites

Before troubleshooting, ensure you have:

- ✅ Windows 10/11 or Windows Server 2019+
- ✅ .NET 9.0 SDK installed
- ✅ Administrator privileges (for some operations)
- ✅ Access to `launcher-logs/` directory

---

## Launcher Won't Start

### Symptoms
- Double-clicking launcher does nothing
- Launcher closes immediately
- "Access is denied" error

### Diagnosis

1. **Check if .NET is installed:**
   ```batch
   dotnet --version
   ```
   Expected: `9.0.x`

2. **Check file permissions:**
   - Right-click launcher → Properties → Security
   - Ensure your user has "Read & Execute" permissions

3. **Check for antivirus blocking:**
   - Check Windows Defender logs
   - Check third-party antivirus logs

### Resolution

#### If .NET is not installed:
1. Download .NET 9.0 SDK from https://dotnet.microsoft.com/download
2. Install with default options
3. Restart terminal
4. Verify: `dotnet --version`

#### If permissions are wrong:
1. Right-click launcher → Properties → Security
2. Click "Edit" → Add your user
3. Grant "Read & Execute" permissions
4. Click OK

#### If antivirus is blocking:
1. Add launcher directory to antivirus exclusions
2. Add `GGs\` directory to exclusions
3. Restart launcher

---

## Build Failures

### Symptoms
- Launcher shows "[ERROR] Build failed!"
- Build takes too long (>5 minutes)
- Compiler errors in log

### Diagnosis

1. **Check build log:**
   ```batch
   type launcher-logs\desktop-*.log
   ```

2. **Try manual build:**
   ```batch
   cd GGs
   dotnet build GGs.sln -c Release
   ```

3. **Check for missing dependencies:**
   ```batch
   dotnet restore GGs\GGs.sln
   ```

### Resolution

#### If NuGet restore fails:
1. Clear NuGet cache:
   ```batch
   dotnet nuget locals all --clear
   ```
2. Restore again:
   ```batch
   dotnet restore GGs\GGs.sln
   ```

#### If compiler errors:
1. Check error message in log
2. Common issues:
   - **CS0246 (Type not found):** Missing NuGet package
   - **CS1061 (Method not found):** API breaking change
   - **CS8600 (Nullable warning):** Null reference issue

3. Fix errors in source code
4. Rebuild:
   ```batch
   dotnet build GGs\GGs.sln -c Release
   ```

#### If build is slow:
1. Clean solution:
   ```batch
   dotnet clean GGs\GGs.sln
   ```
2. Delete `bin/` and `obj/` directories:
   ```batch
   rmdir /s /q GGs\bin GGs\obj
   ```
3. Rebuild

---

## App Launch Failures

### Symptoms
- Launcher shows "[ERROR] Desktop application failed to start"
- Process not found after launch
- App crashes immediately

### Diagnosis

1. **Check if process is running:**
   ```batch
   tasklist | findstr GGs.Desktop.exe
   ```

2. **Check application logs:**
   ```batch
   type GGs\clients\GGs.Desktop\bin\Release\net9.0-windows\logs\*.log
   ```

3. **Try manual launch:**
   ```batch
   cd GGs\clients\GGs.Desktop\bin\Release\net9.0-windows
   GGs.Desktop.exe
   ```

### Resolution

#### If executable not found:
1. Verify build completed:
   ```batch
   dir GGs\clients\GGs.Desktop\bin\Release\net9.0-windows\GGs.Desktop.exe
   ```
2. If missing, rebuild:
   ```batch
   dotnet build GGs\clients\GGs.Desktop\GGs.Desktop.csproj -c Release
   ```

#### If app crashes immediately:
1. Check Event Viewer:
   - Windows Logs → Application
   - Look for .NET Runtime errors
2. Common issues:
   - **Missing dependencies:** Install .NET Desktop Runtime
   - **Configuration error:** Check `appsettings.json`
   - **Database error:** Check database connection string

#### If "Access is denied":
1. Run launcher as Administrator:
   - Right-click launcher → Run as administrator
2. Check file permissions on executable
3. Check antivirus logs

---

## Test Failures

### Symptoms
- Launcher shows "[WARNING] Some tests failed"
- Tests timeout
- Flaky tests

### Diagnosis

1. **Run tests manually:**
   ```batch
   dotnet test GGs\GGs.sln -c Release --verbosity detailed
   ```

2. **Check test logs:**
   ```batch
   type launcher-logs\all-*.log
   ```

3. **Run specific test:**
   ```batch
   dotnet test GGs\tests\GGs.Enterprise.Tests\GGs.Enterprise.Tests.csproj --filter "FullyQualifiedName~TestName"
   ```

### Resolution

#### If tests timeout:
1. Increase timeout in test code
2. Check for deadlocks in application code
3. Run tests with more verbosity to see where it hangs

#### If tests are flaky:
1. Identify flaky tests:
   ```batch
   dotnet test GGs\GGs.sln --logger "console;verbosity=detailed" | findstr "Failed"
   ```
2. Fix race conditions in test code
3. Add proper test isolation
4. Use `[Collection]` attribute for test ordering

#### If tests fail consistently:
1. Check error message
2. Fix failing test or application code
3. Verify fix:
   ```batch
   dotnet test GGs\GGs.sln -c Release
   ```

---

## Performance Issues

### Symptoms
- Launcher takes >2 minutes
- Build takes >5 minutes
- App launch takes >30 seconds

### Diagnosis

1. **Check system resources:**
   - Task Manager → Performance
   - CPU usage should be <80%
   - Memory usage should be <80%
   - Disk usage should be <90%

2. **Check for background processes:**
   ```batch
   tasklist | findstr "msbuild dotnet"
   ```

3. **Check disk I/O:**
   - Task Manager → Performance → Disk
   - Should be <100 MB/s during build

### Resolution

#### If CPU is maxed out:
1. Close unnecessary applications
2. Wait for background tasks to complete
3. Consider upgrading hardware

#### If memory is maxed out:
1. Close unnecessary applications
2. Restart computer
3. Consider adding more RAM

#### If disk is slow:
1. Check for disk errors:
   ```batch
   chkdsk C: /f
   ```
2. Defragment disk (HDD only):
   ```batch
   defrag C: /O
   ```
3. Consider upgrading to SSD

#### If build is slow:
1. Enable parallel builds:
   ```batch
   dotnet build GGs\GGs.sln -c Release -m
   ```
2. Use incremental builds (don't clean every time)
3. Add more CPU cores

---

## Escalation

If the issue persists after following this runbook:

1. **Collect diagnostic information:**
   ```batch
   Test-Launchers.bat
   ```
   Save the log file from `launcher-logs/smoke-test-*.log`

2. **Create GitHub issue:**
   - Go to https://github.com/vasterasstaden/ggsdeepagent/issues
   - Click "New Issue"
   - Use template: "Launcher Issue"
   - Attach log file

3. **Contact support:**
   - Email: support@ggsdeepagent.local
   - Include:
     - Log files from `launcher-logs/`
     - System information (`systeminfo`)
     - Steps to reproduce

---

## Rollback Procedures

If launchers are completely broken:

1. **Restore from backup:**
   ```batch
   git checkout HEAD~1 Launch-*.bat
   ```

2. **Use previous version:**
   - Download previous release from GitHub
   - Extract launchers
   - Replace current launchers

3. **Manual launch:**
   ```batch
   cd GGs\clients\GGs.Desktop\bin\Release\net9.0-windows
   GGs.Desktop.exe
   ```

---

## Monitoring & Alerts

### Key Metrics

- **Build Success Rate:** >95%
- **Test Pass Rate:** 100%
- **Launch Success Rate:** >99%
- **Build Time:** <60 seconds
- **Test Time:** <30 seconds

### Alerts

Set up alerts for:
- Build failures (>2 in a row)
- Test failures (any)
- Launch failures (>3 in a row)
- Performance degradation (>2x normal time)

---

## References

- [ADR-001: Batch File Launchers](ADR-001-Batch-File-Launchers.md)
- [EliNextSteps - Phase 2: Launchers 2.0](../EliNextSteps)
- [Test-Launchers.bat](../Test-Launchers.bat)

---

**Last Reviewed:** 2025-10-03  
**Next Review:** 2025-11-03  
**Owner:** GGsDeepAgent Team

