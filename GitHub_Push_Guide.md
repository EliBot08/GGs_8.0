# GGs Project - GitHub Push Guide
# Step-by-step instructions for pushing to GitHub in batches

## Step 1: Initialize Git Repository
```
cd "C:\Users\307824\OneDrive - Västerås Stad\Skrivbordet\GGs"
git init
git config user.name "Your Name"
git config user.email "your.email@example.com"
```

## Step 2: Configure Remote Repository
```
git remote add origin https://github.com/EliBot08/GGs_5.0.git
```

## Step 3: Add .gitignore (IMPORTANT - Exclude build artifacts)
```
# Create .gitignore file with this content:
bin/
obj/
*.user
*.suo
*.tmp
*.log
.vs/
packages/
node_modules/
build_output.txt
desktop_build.txt
*.exe
*.dll
```

## Step 4: Push in Batches (GitHub Limit: ~100MB per push recommended)

### Batch 1: Core Solution Files
```
git add GGs.sln
git add Directory.Build.targets
git add .gitignore
git commit -m "Initial commit: Core solution files"
git push origin main
```

### Batch 2: Source Code (agents, clients, server, shared)
```
git add agents/
git add clients/
git add server/
git add shared/
git add src/
git commit -m "Add core application source code"
git push origin main
```

### Batch 3: Tests
```
git add tests/
git commit -m "Add test projects"
git push origin main
```

### Batch 4: Documentation and Scripts
```
git add docs/
git add packaging/
git add tools/
git add *.md
git add *.bat
git add *.ps1
git add *.cmd
git commit -m "Add documentation and build scripts"
git push origin main
```

### Batch 5: Configuration Files
```
git add *.json
git add *.xml
git add *.config
git commit -m "Add configuration files"
git push origin main
```

## Step 5: Verify Everything is Pushed
```
git status
git log --oneline
```

## GitHub Limits to Be Aware Of:
- Single file limit: 100MB
- Repository size limit: 5GB (recommended < 1GB for performance)
- Push size: Keep under 1GB per push
- Files per repository: No strict limit, but large numbers can slow down operations

## If You Get Errors:
```
# If push fails due to large files:
git filter-branch --tree-filter 'rm -f path/to/large/file' HEAD
git push origin main --force

# Check repository size:
git count-objects -vH
```

## Final Verification:
After all batches are pushed, verify at: https://github.com/EliBot08/GGs_5.0
