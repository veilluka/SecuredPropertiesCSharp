#!/usr/bin/env pwsh
# Build script for creating standalone executables

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet('win-x64', 'win-arm64', 'linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64', 'all')]
    [string]$Runtime = 'win-x64',
    
    [Parameter(Mandatory=$false)]
    [ValidateSet('self-contained', 'framework-dependent', 'trimmed')]
    [string]$Type = 'self-contained',
    
    [Parameter(Mandatory=$false)]
    [switch]$Clean
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  SecuredPropertiesCSharp - Executable Builder" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Clean if requested
if ($Clean) {
    Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
    if (Test-Path "bin") { Remove-Item -Recurse -Force "bin" }
    if (Test-Path "obj") { Remove-Item -Recurse -Force "obj" }
    Write-Host "Clean complete." -ForegroundColor Green
    Write-Host ""
}

function Build-For-Runtime {
    param(
        [string]$RuntimeId,
        [string]$BuildType
    )
    
    Write-Host "Building for $RuntimeId ($BuildType)..." -ForegroundColor Green
    
    $publishArgs = @(
        'publish',
        '-c', 'Release',
        '-r', $RuntimeId
    )
    
    switch ($BuildType) {
        'self-contained' {
            $publishArgs += '--self-contained', 'true'
            $publishArgs += '/p:PublishSingleFile=true'
            $publishArgs += '/p:IncludeNativeLibrariesForSelfExtract=true'
        }
        'framework-dependent' {
            $publishArgs += '--self-contained', 'false'
            $publishArgs += '/p:PublishSingleFile=true'
        }
        'trimmed' {
            $publishArgs += '--self-contained', 'true'
            $publishArgs += '/p:PublishSingleFile=true'
            $publishArgs += '/p:PublishTrimmed=true'
            $publishArgs += '/p:TrimMode=partial'
        }
    }
    
    & dotnet @publishArgs
    
    if ($LASTEXITCODE -eq 0) {
        $publishPath = "bin\Release\net10.0\$RuntimeId\publish"
        $exeName = if ($RuntimeId.StartsWith('win')) { 
            "SecuredPropertiesCSharp.exe" 
        } else { 
            "SecuredPropertiesCSharp" 
        }
        
        $exePath = Join-Path $publishPath $exeName
        
        if (Test-Path $exePath) {
            $fileSize = (Get-Item $exePath).Length
            $fileSizeMB = [math]::Round($fileSize / 1MB, 2)
            
            Write-Host "✓ Build successful!" -ForegroundColor Green
            Write-Host "  Location: $publishPath" -ForegroundColor Gray
            Write-Host "  Size: $fileSizeMB MB" -ForegroundColor Gray
            Write-Host ""
        }
    } else {
        Write-Host "✗ Build failed!" -ForegroundColor Red
        Write-Host ""
    }
}

# Build for specified runtime(s)
if ($Runtime -eq 'all') {
    $runtimes = @('win-x64', 'win-arm64', 'linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64')
    
    foreach ($rt in $runtimes) {
        Build-For-Runtime -RuntimeId $rt -BuildType $Type
    }
    
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "  All builds complete!" -ForegroundColor Cyan
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
} else {
    Build-For-Runtime -RuntimeId $Runtime -BuildType $Type
    
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "  Build complete!" -ForegroundColor Cyan
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "Usage examples:" -ForegroundColor Yellow
Write-Host "  .\build-exe.ps1                          # Build for Windows x64 (self-contained)"
Write-Host "  .\build-exe.ps1 -Runtime linux-x64       # Build for Linux x64"
Write-Host "  .\build-exe.ps1 -Type trimmed            # Build trimmed version"
Write-Host "  .\build-exe.ps1 -Runtime all             # Build for all platforms"
Write-Host "  .\build-exe.ps1 -Clean                   # Clean before building"
Write-Host ""
