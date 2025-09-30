@echo off
echo ================================================
echo GGs Project - GitHub Push Script
echo ================================================
echo This script will push your GGs project to GitHub in batches
echo to avoid GitHub's push size limits.
echo.
echo Repository: https://github.com/EliBot08/GGs_5.0
echo.

set REPO_URL=https://github.com/EliBot08/GGs_5.0.git
set BRANCH=main

echo Step 1: Initialize Git repository...
git init
if %errorlevel% neq 0 (
    echo ERROR: Failed to initialize git
    pause
    exit /b 1
)

echo Step 2: Configure Git...
git config user.name "GGs Developer"
git config user.email "ggs@example.com"

echo Step 3: Add remote repository...
git remote add origin %REPO_URL%
if %errorlevel% neq 0 (
    echo WARNING: Remote might already exist, continuing...
)

echo Step 4: Add .gitignore...
git add .gitignore
git commit -m "Add .gitignore file"
echo.

echo ================================================
echo PUSHING IN BATCHES (Safe for GitHub limits)
echo ================================================
echo.

echo Batch 1/5: Core solution files...
git add GGs.sln
git add Directory.Build.targets
git add .gitignore
git commit -m "Batch 1: Core solution files and build configuration"
echo Pushing Batch 1...
git push origin %BRANCH%
if %errorlevel% neq 0 (
    echo ERROR: Failed to push Batch 1
    pause
    exit /b 1
)
echo ✓ Batch 1 pushed successfully
echo.

echo Batch 2/5: Source code (agents, clients, server, shared)...
git add agents/
git add clients/
git add server/
git add shared/
git add src/
git commit -m "Batch 2: Core application source code"
echo Pushing Batch 2...
git push origin %BRANCH%
if %errorlevel% neq 0 (
    echo ERROR: Failed to push Batch 2
    pause
    exit /b 1
)
echo ✓ Batch 2 pushed successfully
echo.

echo Batch 3/5: Tests...
git add tests/
git commit -m "Batch 3: Test projects and test files"
echo Pushing Batch 3...
git push origin %BRANCH%
if %errorlevel% neq 0 (
    echo ERROR: Failed to push Batch 3
    pause
    exit /b 1
)
echo ✓ Batch 3 pushed successfully
echo.

echo Batch 4/5: Documentation and scripts...
git add docs/
git add packaging/
git add tools/
git add *.md
git add *.bat
git add *.ps1
git add *.cmd
git add *.txt
git add *.html
git add *.vbs
git commit -m "Batch 4: Documentation, packaging, and build scripts"
echo Pushing Batch 4...
git push origin %BRANCH%
if %errorlevel% neq 0 (
    echo ERROR: Failed to push Batch 4
    pause
    exit /b 1
)
echo ✓ Batch 4 pushed successfully
echo.

echo Batch 5/5: Configuration and remaining files...
git add *.json
git add *.xml
git add *.config
git add .github/
git add *
git commit -m "Batch 5: Configuration files and remaining assets"
echo Pushing Batch 5...
git push origin %BRANCH%
if %errorlevel% neq 0 (
    echo ERROR: Failed to push Batch 5
    pause
    exit /b 1
)
echo ✓ Batch 5 pushed successfully
echo.

echo ================================================
echo VERIFICATION
echo ================================================
echo.

echo Checking repository status...
git status
echo.
echo Recent commits:
git log --oneline -5
echo.

echo ================================================
echo SUCCESS: All batches pushed to GitHub!
echo ================================================
echo.
echo Repository URL: https://github.com/EliBot08/GGs_5.0
echo Branch: %BRANCH%
echo.
echo Verify at: https://github.com/EliBot08/GGs_5.0
echo.
echo Press any key to exit...
pause >nul
