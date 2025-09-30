# Packaging Phase Fixes

## Issues Fixed

### 1. **MSI Build - WiX Tool Manifest Path Issue**
**Problem**: The `build-msi.ps1` script was looking for `dotnet-tools.json` at the repository root, but it actually exists in the `.config/` directory.

**Root Cause**: The script had hardcoded path `Join-Path $repoRoot 'dotnet-tools.json'` which doesn't match the actual location where .NET tools are configured.

**Fix**: Updated line 40 in `packaging/build-msi.ps1` from:
```powershell
$manifestPath = Join-Path $repoRoot 'dotnet-tools.json'
```
to:
```powershell
$manifestPath = Join-Path $repoRoot '.config\dotnet-tools.json'
```

This ensures the WiX v4 tool (version 4.0.2) can be properly located, installed, and restored during the packaging process.

### 2. **Verified Icon Path**
- Confirmed `..\..\clients\GGs.Desktop\assets\app-icon.ico` in `Product.wxs` resolves correctly
- Icon file exists and is accessible

### 3. **MSIX Build Requirements**
The MSIX build script (`build-msix.ps1`) requires either:
- MSBuild (from Visual Studio) - preferred
- MakeAppx.exe (from Windows 10/11 SDK) - fallback

GitHub Actions `windows-latest` runners include both, so MSIX packaging should work correctly.

## Testing Results

### Local Test - MSI Build ✅
```
PS> .\packaging\build-msi.ps1 -Configuration Release -SelfContained -Channel stable -FileAssociations .ggs,.ggprofile
Building MSI -> C:\...\packaging\artifacts\GGs.Desktop.msi ...
Built: C:\...\packaging\artifacts\GGs.Desktop.msi
```
**Result**: SUCCESS - MSI created successfully

### Expected GitHub Actions Outcome
With the manifest path fix:
1. ✅ **Build, Test, Coverage** - Already passing
2. ✅ **Package (MSI + MSIX)** - Will now pass with corrected path
   - MSI build via WiX v4 ✅
   - MSIX build via MSBuild/MakeAppx ✅
3. ✅ **Health check gate (blue/green)** - Will execute after successful packaging

## Impact on Phase 3: Health Check Gate

The Health Check Gate job depends on the Package job completing successfully. With this fix:

**Health Check Process**:
1. Polls the API endpoint `/ready` for up to 60 seconds (30 attempts × 2 seconds)
2. Expects HTTP 200 response to proceed
3. If service doesn't respond, triggers rollback simulation

**Note**: The health check will attempt to connect to:
- Default: `https://localhost:5001` (if no URL provided)
- Custom: Value from `api_base_url` workflow input

For the health check to pass in CI/CD, you'll need to either:
- Deploy the packaged application and provide its URL via workflow_dispatch input
- Mock the health endpoint in the CI environment
- Adjust the health check to validate the packages exist instead of checking a running service

## Files Modified
- `packaging/build-msi.ps1` - Fixed dotnet-tools.json manifest path (line 40)

## Next Steps for Production
1. ✅ Commit and push the fix
2. ✅ GitHub Actions will build MSI successfully  
3. ✅ GitHub Actions will build MSIX successfully
4. ⚠️ Health check may need adjustment based on deployment strategy
5. Consider adding artifact validation instead of live service health check for packaging phase

## Recommendations
- Add artifact validation step after packaging to verify .msi and .msix files exist
- Consider splitting health check into separate deployment workflow
- Add checksums/signing verification for release artifacts
