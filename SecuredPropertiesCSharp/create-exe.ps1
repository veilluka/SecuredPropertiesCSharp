#!/usr/bin/env pwsh
# Creates a single self-contained executable (no .NET required on target machine)

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet('win-x64', 'win-arm64', 'linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64')]
    [string]$Runtime = 'win-x64',

    [Parameter(Mandatory=$false)]
    [switch]$Clean
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "==================================================================="  -ForegroundColor Cyan
Write-Host "  SecuredPropertiesCSharp - Single-File Executable Builder"            -ForegroundColor Cyan
Write-Host "==================================================================="  -ForegroundColor Cyan
Write-Host ""

# Read version from csproj
$csproj = Join-Path $PSScriptRoot "SecuredPropertiesCSharp.csproj"
[xml]$xml = Get-Content $csproj
$version = $xml.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { $version = "0.0.0" }
Write-Host "  Version: $version" -ForegroundColor White
Write-Host "  Runtime: $Runtime" -ForegroundColor White
Write-Host ""

# Backup original project file
$csprojBackup = $csproj + ".backup"
Copy-Item $csproj $csprojBackup

# Temporarily change to executable for single-file build
Write-Host "Temporarily configuring project as executable..." -ForegroundColor Yellow
$csprojContent = Get-Content $csproj -Raw
$csprojContent = $csprojContent -replace '<OutputType>Library</OutputType>', '<OutputType>Exe</OutputType>'
$csprojContent = $csprojContent -replace 'net48', 'net8.0'  # Use .NET 8.0 for self-contained executable
$csprojContent = $csprojContent -replace '<Compile Remove="Program.cs" />', ''  # Include Program.cs for executable
Set-Content $csproj -Value $csprojContent

# Clean if requested
if ($Clean) {
    Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
    if (Test-Path "bin") { Remove-Item -Recurse -Force "bin" }
    if (Test-Path "obj") { Remove-Item -Recurse -Force "obj" }
    Write-Host "Clean complete." -ForegroundColor Green
    Write-Host ""
}

# Build single-file self-contained executable
$publishArgs = @(
    'publish',
    '-c', 'Release',
    '-r', $Runtime,
    '--self-contained', 'true',
    '/p:PublishSingleFile=true',
    '/p:IncludeNativeLibrariesForSelfExtract=true',
    '/p:IncludeAllContentForSelfExtract=true',
    '/p:DebugType=none'
)

& dotnet @publishArgs

if ($LASTEXITCODE -ne 0) {
    Write-Host "[FAIL] Build failed!" -ForegroundColor Red
    exit 1
}

$publishPath = "bin\Release\net8.0\$Runtime\publish"
if ($Runtime.StartsWith('win')) {
    $exeName = "SecuredPropertiesCSharp.exe"
} else {
    $exeName = "SecuredPropertiesCSharp"
}

$exePath = Join-Path $publishPath $exeName

if (Test-Path $exePath) {
    $fileSize = (Get-Item $exePath).Length
    $fileSizeMB = [math]::Round($fileSize / 1MB, 2)

    # Verify it's truly a single file
    $publishFiles = Get-ChildItem $publishPath -File
    $fileCount = $publishFiles.Count

    Write-Host ""
    Write-Host "[OK] Build successful!" -ForegroundColor Green
    Write-Host "  File:    $exePath" -ForegroundColor Gray
    Write-Host "  Size:    $fileSizeMB MB" -ForegroundColor Gray
    Write-Host "  Version: $version" -ForegroundColor Gray
    Write-Host "  Files:   $fileCount (in publish folder)" -ForegroundColor Gray

    if ($fileCount -gt 1) {
        Write-Host ""
        Write-Host "  [WARN] Expected 1 file but found $fileCount. Extra files:" -ForegroundColor Yellow
        foreach ($f in $publishFiles) {
            if ($f.Name -ne $exeName) {
                Write-Host "    - $($f.Name)" -ForegroundColor Yellow
            }
        }
    }
    
    # Restore original project file
    Write-Host "Restoring original project configuration..." -ForegroundColor Yellow
    Move-Item $csprojBackup $csproj -Force
    Write-Host "[OK] Project restored to library configuration" -ForegroundColor Green
} else {
    Write-Host "[FAIL] Executable not found at: $exePath" -ForegroundColor Red
    # Restore backup on failure
    if (Test-Path $csprojBackup) {
        Move-Item $csprojBackup $csproj -Force
    }
    exit 1
}

Write-Host ""
Write-Host "==================================================================="  -ForegroundColor Cyan
Write-Host "  Build complete!"                                                     -ForegroundColor Cyan
Write-Host "==================================================================="  -ForegroundColor Cyan
Write-Host ""
