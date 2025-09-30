# 🔍 CI/CD Validation Checklist

**Purpose:** Ensure all pipeline phases pass without errors  
**Last Updated:** 2025-10-01 00:57 CET

---

## ✅ Pre-Push Validation (Local)

### Code Quality
- [ ] All files compile without errors (`dotnet build`)
- [ ] No placeholder methods or TODO comments in production code
- [ ] No null returns without proper null handling
- [ ] All async methods have proper error handling
- [ ] All using statements and dependencies resolved

### Package Builds
- [ ] MSI builds successfully: `.\packaging\build-msi.ps1 -Configuration Release -SelfContained`
- [ ] MSI file exists: `packaging\artifacts\GGs.Desktop.msi`
- [ ] MSI size is reasonable (>0.5 MB expected)
- [ ] Manifest validates: `packaging\msix\GGs.Desktop.Package\Package.appxmanifest`
- [ ] No unsupported extension categories in manifest

### Manifest Schema Validation
**Supported Categories (MakeAppx):**
- ✅ `windows.fileTypeAssociation`
- ✅ `windows.protocol`
- ✅ `windows.autoPlayContent`
- ✅ `windows.autoPlayDevice`
- ✅ `windows.shareTarget`
- ✅ `windows.fullTrustProcess` (desktop)
- ❌ `windows.startupTask` (Store-only, NOT supported)

---

## 🔄 Pipeline Phase Checks

### Phase 1: Build, Test, Coverage ✅
**Expected Result:** All tests pass, code compiles

**Validation Points:**
- [ ] `dotnet restore` succeeds for all projects
- [ ] `dotnet build` completes with 0 errors
- [ ] Unit tests execute (`dotnet test`)
- [ ] Code coverage reports generated

**Common Issues:**
- Missing NuGet packages → Check `.csproj` files
- Compilation errors → Fix syntax/type errors
- Test failures → Debug failing test cases

---

### Phase 2: Package (MSI + MSIX) ✅
**Expected Result:** Both MSI and MSIX packages created

#### MSI Build Checks:
- [ ] WiX tool manifest detected (`.config/dotnet-tools.json`)
- [ ] Manifest file unblocked (no MOTW errors)
- [ ] `dotnet wix` command available
- [ ] Component manifest generated (`Harvested.wxs`)
- [ ] All component GUIDs unique
- [ ] Icon path resolved: `$(var.PublishDir)\assets\app-icon.ico`
- [ ] MSI output created: `packaging\artifacts\GGs.Desktop.msi`

**Common Issues:**
- `heat` command not found → Use custom manifest generator (already implemented)
- Duplicate component GUIDs → Ensure unique GUIDs per component
- Icon not found → Use publish directory variable path
- `Absent="disallow"` error → Remove invalid attributes

#### MSIX Build Checks:
- [ ] Manifest file exists: `Package.appxmanifest` (NOT `.xml`)
- [ ] Only supported extension categories used
- [ ] Assets directory exists with required images:
  - `Square150x150Logo.png`
  - `Square44x44Logo.png`
  - `StoreLogo.png`
- [ ] MakeAppx or MSBuild available
- [ ] MSIX output created: `packaging\artifacts\msix\GGs.Desktop.msix`

**Common Issues:**
- Manifest not found → Check filename (no `.xml` extension)
- `windows.startupTask` error → Remove (not supported)
- Schema validation error → Check extension categories
- Missing assets → Ensure PNG files exist

---

### Phase 3: Health Check Gate ✅
**Expected Result:** Packages validated, ready for deployment

**Validation Points:**
- [ ] Artifacts uploaded to workflow storage
- [ ] MSI file size > 0.5 MB
- [ ] MSIX file size > 0 bytes
- [ ] No corruption errors

---

### Phase 4: Deploy 🔄
**Expected Result:** Packages deployed to target environment

**Validation Points:**
- [ ] Target server accessible
- [ ] Credentials configured
- [ ] Deployment script executes
- [ ] Health check endpoints respond

---

## 🛡️ Error Prevention Guidelines

### 1. Manifest Schema Compliance
**Rule:** Never add extension categories without verifying MakeAppx support

**Validation:**
```powershell
# Check manifest against schema
$manifest = Get-Content "packaging\msix\GGs.Desktop.Package\Package.appxmanifest"
if ($manifest -match 'windows\.startupTask') {
    throw "Unsupported extension: windows.startupTask"
}
```

### 2. Component GUID Uniqueness
**Rule:** Every WiX component must have a unique GUID

**Implementation:**
```powershell
# Already implemented in build-msi.ps1
$guid = (New-Guid).ToString('D').ToUpper()
$wxsContent += "`n      <Component Id='$compId' Guid='$guid'>"
```

### 3. File Path Resolution
**Rule:** Use variables for paths that differ between local/CI

**Best Practice:**
```xml
<!-- Good: Variable-based path -->
<Icon Id="AppIcon" SourceFile="$(var.PublishDir)\assets\app-icon.ico" />

<!-- Bad: Relative path -->
<Icon Id="AppIcon" SourceFile="..\..\clients\GGs.Desktop\assets\app-icon.ico" />
```

### 4. Exit Code Validation
**Rule:** Always check exit codes after external commands

**Implementation:**
```powershell
dotnet build
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }
```

### 5. File Existence Checks
**Rule:** Validate file existence before operations

**Implementation:**
```powershell
if (-not (Test-Path $manifestSrc)) { 
    throw "Package manifest not found at: $manifestSrc" 
}
```

---

## 📝 Pre-Commit Checklist

Before pushing to GitHub:

1. **Local Build Test:**
   ```powershell
   dotnet build -c Release
   dotnet test
   .\packaging\build-msi.ps1 -Configuration Release -SelfContained
   ```

2. **Manifest Validation:**
   - Check `Package.appxmanifest` for unsupported categories
   - Verify all asset files exist
   - Ensure filename is correct (no `.xml`)

3. **Code Review:**
   - No placeholders or TODOs
   - No null returns without handling
   - Proper error handling everywhere

4. **Documentation:**
   - Update PACKAGING_COMPLETE.md if needed
   - Document any new changes

---

## 🔧 Quick Fix Commands

### Reset Packaging Artifacts:
```powershell
Remove-Item -Recurse -Force packaging\artifacts\* -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force packaging\msi\_generated\* -ErrorAction SilentlyContinue
```

### Rebuild Everything:
```powershell
dotnet clean
dotnet restore
dotnet build -c Release
.\packaging\build-msi.ps1 -Configuration Release -SelfContained
```

### Validate Manifest Schema:
```powershell
$manifest = [xml](Get-Content "packaging\msix\GGs.Desktop.Package\Package.appxmanifest")
$extensions = $manifest.Package.Applications.Application.Extensions.Extension
$extensions | ForEach-Object { Write-Host "Category: $($_.Category)" }
```

---

## ✅ Success Criteria

### All Phases Pass When:
1. ✅ Code compiles without errors
2. ✅ All tests pass
3. ✅ MSI builds successfully (0.54+ MB)
4. ✅ MSIX builds successfully
5. ✅ No schema validation errors
6. ✅ All artifacts uploaded to workflow
7. ✅ Health check validates packages

### Known Working State (as of 2025-10-01):
- **MSI:** Custom WiX v4 manifest generation
- **MSIX:** Cleaned manifest with only supported extensions
- **Commits:** `b0eb980`, `3d10f23`, `6b3c0a8`, `1f0b9be`
- **Status:** Ready for CI/CD ✅

---

## 🚨 Emergency Rollback

If pipeline fails unexpectedly:

```bash
# Revert to last known good commit
git revert HEAD
git push origin main

# Or reset to specific working commit
git reset --hard 1f0b9be
git push origin main --force
```

---

## 📞 Support & Documentation

- **MSI Packaging:** See `PACKAGING_COMPLETE.md`
- **WiX v4 Details:** `packaging/build-msi.ps1` (comments)
- **MSIX Schema:** `packaging/msix/GGs.Desktop.Package/Package.appxmanifest`
- **GitHub Actions:** `.github/workflows/ci.yml`

---

**Remember:** Always test locally before pushing! 🎯
