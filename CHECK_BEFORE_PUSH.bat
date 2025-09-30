@echo off
echo ================================================
echo GGs Project - Git Push Preparation Check
echo ================================================
echo This script checks what files will be included
echo in your GitHub repository push.
echo.

echo Initializing git (if not already done)...
git init >nul 2>&1
git config user.name "GGs Developer" >nul 2>&1
git config user.email "ggs@example.com" >nul 2>&1

echo.
echo Files that will be tracked (after .gitignore):
echo --------------------------------------------------
git add . --dry-run | findstr /v "warning\|fatal" | head -20

echo.
echo Total file count estimate:
powershell -Command "Get-ChildItem -Path '.' -Recurse -File | Where-Object { $_.FullName -notmatch '(\\bin\\|\\obj\\|\\packages\\|\\node_modules\\|\.git\\)' } | Measure-Object | Select-Object -ExpandProperty Count" 2>nul

echo.
echo Large files that might cause issues:
powershell -Command "Get-ChildItem -Path '.' -Recurse -File | Where-Object { $_.Length -gt 50MB } | Select-Object FullName, @{Name='SizeMB';Expression={[math]::Round($_.Length/1MB,2)}} | Format-Table -AutoSize" 2>nul

echo.
echo Repository size estimate:
powershell -Command "$size = (Get-ChildItem -Path '.' -Recurse -File | Where-Object { $_.FullName -notmatch '(\\bin\\|\\obj\\|\\packages\\|\\node_modules\\|\.git\\)' } | Measure-Object -Property Length -Sum).Sum; [math]::Round($size/1MB,2)" 2>nul
echo MB (excluding build artifacts)

echo.
echo ================================================
echo If size is over 1GB, consider using Git LFS for large files
echo or remove unnecessary files before pushing.
echo ================================================
echo.
pause
