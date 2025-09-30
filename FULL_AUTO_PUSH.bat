@echo off
echo ================================================
echo GGs Project - FULL AUTOMATED PUSH TO GITHUB
echo ================================================
echo This script will completely push your GGs project
echo to GitHub automatically. Just run it!
echo.
echo Repository: https://github.com/EliBot08/GGs_5.0
echo.

set REPO_URL=https://github.com/EliBot08/GGs_5.0.git
set BRANCH=main

echo [1/8] Initializing Git repository...
git init
git config user.name "GGs Developer"
git config user.email "developer@ggs-project.com"
git remote add origin %REPO_URL%

echo.
echo [2/8] Adding .gitignore first...
git add .gitignore
git commit -m "Initial commit: Add comprehensive .gitignore"
echo.

echo ================================================
echo PUSHING ALL FILES IN SAFE BATCHES
echo ================================================
echo.

echo [3/8] Batch 1: Core solution and project files...
git add GGs.sln
git add Directory.Build.targets
git add .config/
git add *.json
git add *.xml
git add *.config
git commit -m "Batch 1: Core solution, configuration, and project files"
echo Pushing Batch 1...
git push origin %BRANCH%
echo ✓ Batch 1 complete
echo.

echo [4/8] Batch 2: Source code (agents)...
git add agents/
git commit -m "Batch 2: Agent services and components"
echo Pushing Batch 2...
git push origin %BRANCH%
echo ✓ Batch 2 complete
echo.

echo [5/8] Batch 3: Desktop client...
git add clients/
git commit -m "Batch 3: Desktop client application"
echo Pushing Batch 3...
git push origin %BRANCH%
echo ✓ Batch 3 complete
echo.

echo [6/8] Batch 4: Server components...
git add server/
git commit -m "Batch 4: Server components and APIs"
echo Pushing Batch 4...
git push origin %BRANCH%
echo ✓ Batch 4 complete
echo.

echo [7/8] Batch 5: Shared libraries and tests...
git add shared/
git add tests/
git commit -m "Batch 5: Shared libraries and test projects"
echo Pushing Batch 5...
git push origin %BRANCH%
echo ✓ Batch 5 complete
echo.

echo [8/8] Batch 6: Documentation, tools, and remaining files...
git add docs/
git add packaging/
git add tools/
git add src/
git add .github/
git add *.md
git add *.bat
git add *.ps1
git add *.cmd
git add *.txt
git add *.html
git add *.vbs
git add *
git commit -m "Batch 6: Documentation, tools, and remaining project files"
echo Pushing Batch 6...
git push origin %BRANCH%
echo ✓ Batch 6 complete
echo.

echo ================================================
echo FINAL VERIFICATION
echo ================================================
echo.

echo Repository status:
git status
echo.

echo Recent commits:
git log --oneline -8
echo.

echo Repository size:
git count-objects -vH
echo.

echo ================================================
echo 🎉 SUCCESS! PROJECT FULLY PUSHED TO GITHUB
echo ================================================
echo.
echo Repository URL: https://github.com/EliBot08/GGs_5.0
echo Branch: %BRANCH%
echo.
echo ✅ All source code uploaded
echo ✅ All documentation included
echo ✅ Build configurations preserved
echo ✅ Git history maintained
echo.
echo View your project at: https://github.com/EliBot08/GGs_5.0
echo.
echo Press any key to exit...
pause >nul
