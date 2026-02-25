#!/usr/bin/env pwsh
# Builds single-file exe, creates a GitHub release with version tag, and uploads the artifact.
#
# Prerequisites: gh CLI (https://cli.github.com) must be installed and authenticated.
#
# Usage:
#   .\release-exe.ps1 -Version 1.0.0
#   .\release-exe.ps1 -Version 1.2.0 -Runtime win-x64
#   .\release-exe.ps1 -Version 2.0.0 -Runtime win-x64,linux-x64

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,

    [Parameter(Mandatory=$false)]
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "==================================================================="  -ForegroundColor Cyan
Write-Host "  SecuredPropertiesCSharp - Release Builder"                            -ForegroundColor Cyan
Write-Host "==================================================================="  -ForegroundColor Cyan
Write-Host ""

# Validate version format
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    Write-Host "[FAIL] Version must be in format X.Y.Z (e.g., 1.0.0)" -ForegroundColor Red
    exit 1
}

# Check gh CLI is available
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Host "[FAIL] GitHub CLI (gh) is not installed. Install from https://cli.github.com" -ForegroundColor Red
    exit 1
}

# Check for uncommitted changes
$gitStatus = git status --porcelain
if ($gitStatus) {
    Write-Host "[WARN] You have uncommitted changes:" -ForegroundColor Yellow
    Write-Host $gitStatus -ForegroundColor Gray
    Write-Host ""
    $confirm = Read-Host "Continue anyway? (y/N)"
    if ($confirm -ne 'y') {
        Write-Host "Aborted." -ForegroundColor Yellow
        exit 0
    }
}

# Step 1: Update version in csproj
Write-Host "Step 1: Updating version to $Version..." -ForegroundColor Green
$csproj = Join-Path $PSScriptRoot "SecuredPropertiesCSharp.csproj"
$content = Get-Content $csproj -Raw
$content = $content -replace '<Version>[^<]+</Version>',        "<Version>$Version</Version>"
$content = $content -replace '<AssemblyVersion>[^<]+</AssemblyVersion>', "<AssemblyVersion>$Version.0</AssemblyVersion>"
$content = $content -replace '<FileVersion>[^<]+</FileVersion>',        "<FileVersion>$Version.0</FileVersion>"
Set-Content $csproj $content -NoNewline
Write-Host "  Updated csproj to version $Version" -ForegroundColor Gray

# Step 2: Build for each runtime
$runtimes = $Runtime -split ','
$artifacts = @()

foreach ($rt in $runtimes) {
    $rt = $rt.Trim()
    Write-Host ""
    Write-Host "Step 2: Building for $rt..." -ForegroundColor Green

    $publishArgs = @(
        'publish',
        '-c', 'Release',
        '-r', $rt,
        '--self-contained', 'true',
        '/p:PublishSingleFile=true',
        '/p:IncludeNativeLibrariesForSelfExtract=true',
        '/p:IncludeAllContentForSelfExtract=true',
        '/p:DebugType=none'
    )

    & dotnet @publishArgs

    if ($LASTEXITCODE -ne 0) {
        Write-Host "[FAIL] Build failed for $rt!" -ForegroundColor Red
        exit 1
    }

    $publishPath = "bin\Release\net10.0\$rt\publish"
    if ($rt.StartsWith('win')) {
        $exeName = "SecuredPropertiesCSharp.exe"
    } else {
        $exeName = "SecuredPropertiesCSharp"
    }

    $exePath = Join-Path $publishPath $exeName

    if (-not (Test-Path $exePath)) {
        Write-Host "[FAIL] Executable not found: $exePath" -ForegroundColor Red
        exit 1
    }

    # Rename with version and runtime for the release asset
    $ext = ""
    if ($rt.StartsWith('win')) { $ext = ".exe" }
    $assetName = "SecuredPropertiesCSharp-$Version-$rt$ext"
    $assetPath = Join-Path $publishPath $assetName
    Copy-Item $exePath $assetPath -Force

    $fileSize = (Get-Item $assetPath).Length
    $fileSizeMB = [math]::Round($fileSize / 1MB, 2)
    Write-Host "  [OK] $assetName ($fileSizeMB MB)" -ForegroundColor Gray

    $artifacts += $assetPath
}

# Step 3: Commit version change
Write-Host ""
Write-Host "Step 3: Committing version change..." -ForegroundColor Green
git add $csproj
git commit -m "Release v$Version`n`nCo-Authored-By: Oz <oz-agent@warp.dev>"
if ($LASTEXITCODE -ne 0) {
    Write-Host "  [WARN] Nothing to commit (version may already be set)" -ForegroundColor Yellow
}

# Step 4: Create git tag
Write-Host ""
Write-Host "Step 4: Creating tag v$Version..." -ForegroundColor Green
git tag -a "v$Version" -m "Release v$Version"
if ($LASTEXITCODE -ne 0) {
    Write-Host "[FAIL] Failed to create tag. Does v$Version already exist?" -ForegroundColor Red
    exit 1
}

# Step 5: Push commit and tag
Write-Host ""
Write-Host "Step 5: Pushing to remote..." -ForegroundColor Green
git push
git push --tags

# Step 6: Create GitHub release
Write-Host ""
Write-Host "Step 6: Creating GitHub release v$Version..." -ForegroundColor Green

$releaseNotes = @"
## SecuredPropertiesCSharp v$Version

### Downloads
Self-contained single-file executables (no .NET runtime required):

| Platform | File |
|----------|------|
"@

foreach ($rt in $runtimes) {
    $rt = $rt.Trim()
    $ext = ""
    if ($rt.StartsWith('win')) { $ext = ".exe" }
    $assetName = "SecuredPropertiesCSharp-$Version-$rt$ext"
    $releaseNotes += "| $rt | $assetName |`n"
}

$releaseNotes += @"

### Usage
``````
SecuredPropertiesCSharp -help
SecuredPropertiesCSharp -init myconfig.properties
``````
"@

$ghArgs = @(
    'release', 'create',
    "v$Version",
    '--title', "v$Version",
    '--notes', $releaseNotes
)

foreach ($a in $artifacts) {
    $ghArgs += $a
}

& gh @ghArgs

if ($LASTEXITCODE -ne 0) {
    Write-Host "[FAIL] Failed to create GitHub release!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "==================================================================="  -ForegroundColor Cyan
Write-Host "  Release v$Version published!"                                        -ForegroundColor Cyan
Write-Host "==================================================================="  -ForegroundColor Cyan
Write-Host ""
Write-Host "  Tag:     v$Version" -ForegroundColor Gray
Write-Host "  Assets:  $($artifacts.Count) file(s)" -ForegroundColor Gray
Write-Host ""
