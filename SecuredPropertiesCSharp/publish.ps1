#!/usr/bin/env pwsh
# Publish script for creating distributable packages

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet('win-x64', 'win-arm64', 'linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64', 'all')]
    [string]$Runtime = 'win-x64',
    
    [Parameter(Mandatory=$false)]
    [ValidateSet('self-contained', 'trimmed')]
    [string]$Type = 'trimmed',
    
    [Parameter(Mandatory=$false)]
    [string]$OutputFolder = "..\..\..\dist"
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "     SecuredPropertiesCSharp - Distribution Publisher          " -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

# Create output directory
$outputPath = Join-Path $PSScriptRoot $OutputFolder
if (!(Test-Path $outputPath)) {
    New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
}

# Get version from project file
$version = "1.0.0"
if (Test-Path "SecuredPropertiesCSharp.csproj") {
    $csproj = Get-Content "SecuredPropertiesCSharp.csproj" -Raw
    if ($csproj -match '<Version>(.*?)</Version>') {
        $version = $matches[1]
    }
}

Write-Host "Version: $version" -ForegroundColor Yellow
Write-Host "Output: $outputPath" -ForegroundColor Yellow
Write-Host ""

function Publish-For-Runtime {
    param(
        [string]$RuntimeId,
        [string]$BuildType
    )
    
    Write-Host "================================================================" -ForegroundColor Green
    Write-Host "Publishing for: $RuntimeId ($BuildType)" -ForegroundColor Green
    Write-Host "================================================================" -ForegroundColor Green
    
    # Clean previous builds (skip if locked)
    Write-Host "Cleaning..." -ForegroundColor Yellow
    try {
        if (Test-Path "bin") { Remove-Item -Recurse -Force "bin" -ErrorAction SilentlyContinue }
        if (Test-Path "obj") { Remove-Item -Recurse -Force "obj" -ErrorAction SilentlyContinue }
    } catch {
        Write-Host "  Some files are locked, skipping clean..." -ForegroundColor Yellow
    }
    
    # Build arguments
    $publishArgs = @(
        'publish',
        '-c', 'Release',
        '-r', $RuntimeId
    )
    
    if ($BuildType -eq 'trimmed') {
        $publishArgs += '--self-contained', 'true'
        $publishArgs += '/p:PublishSingleFile=true'
        $publishArgs += '/p:PublishTrimmed=true'
        $publishArgs += '/p:TrimMode=partial'
    } else {
        $publishArgs += '--self-contained', 'true'
        $publishArgs += '/p:PublishSingleFile=true'
        $publishArgs += '/p:IncludeNativeLibrariesForSelfExtract=true'
    }
    
    # Publish
    Write-Host "Building..." -ForegroundColor Yellow
    & dotnet @publishArgs
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[ERROR] Build failed!" -ForegroundColor Red
        return
    }
    
    # Determine executable name
    $exeName = if ($RuntimeId.StartsWith('win')) { 
        "SecuredPropertiesCSharp.exe" 
    } else { 
        "SecuredPropertiesCSharp" 
    }
    
    $publishPath = "bin\Release\net10.0\$RuntimeId\publish"
    $exePath = Join-Path $publishPath $exeName
    
    if (!(Test-Path $exePath)) {
        Write-Host "[ERROR] Executable not found!" -ForegroundColor Red
        return
    }
    
    # Get file size
    $fileSize = (Get-Item $exePath).Length
    $fileSizeMB = [math]::Round($fileSize / 1MB, 2)
    
    Write-Host "[OK] Build successful! Size: $fileSizeMB MB" -ForegroundColor Green
    
    # Create distribution package
    Write-Host "Creating distribution package..." -ForegroundColor Yellow
    
    $packageName = "SecuredPropertiesCSharp-$version-$RuntimeId"
    $packagePath = Join-Path $outputPath $packageName
    
    if (Test-Path $packagePath) {
        Remove-Item -Recurse -Force $packagePath
    }
    New-Item -ItemType Directory -Path $packagePath -Force | Out-Null
    
    # Copy executable
    Copy-Item $exePath -Destination $packagePath
    
    # Create README for the package
    $readmeContent = @"
# Secured Properties CLI Tool
Version: $version
Platform: $RuntimeId
Build Type: $BuildType

## Quick Start

This is a standalone executable. No installation required!

### Basic Commands

Generate a random password:
``````
$exeName -generatePassword
``````

Create a new secure storage:
``````
$exeName -create myconfig.properties -pass YourPassword123!
``````

Add an encrypted property:
``````
$exeName -addSecured myconfig.properties -key app@@api@@key -value secret123 -pass YourPassword123!
``````

Add an unencrypted property:
``````
$exeName -addUnsecured myconfig.properties -key app@@name -value MyApp
``````

Get a property value:
``````
$exeName -getValue myconfig.properties -key app@@api@@key -pass YourPassword123!
``````

Print all properties:
``````
$exeName -print myconfig.properties
``````

Delete a property:
``````
$exeName -delete myconfig.properties -key app@@api@@key -pass YourPassword123!
``````

Show help:
``````
$exeName -help
``````

## Features

- Password-based encryption using AES-256
- Hierarchical property keys (use @@ as separator)
- Mix encrypted and unencrypted properties
- Master password protection
- No dependencies - runs immediately

## Notes

- Master passwords must be at least 12 characters
- Encrypted properties are marked with {ENC} in the file
- Properties files are plain text but encrypted values are secure
- Keep your master password safe - it cannot be recovered!

## Support

For more information, visit:
https://github.com/yourusername/secured-properties

---
Built with .NET 8.0 | © 2024
"@
    
    Set-Content -Path (Join-Path $packagePath "README.txt") -Value $readmeContent
    
    # Create example script for Windows
    if ($RuntimeId.StartsWith('win')) {
        $exampleScript = @"
@echo off
REM Example usage of SecuredPropertiesCSharp

echo ================================================
echo  Secured Properties - Example Usage
echo ================================================
echo.

REM Generate a password
echo Generating a random password:
$exeName -generatePassword
echo.

REM Create storage
echo Creating a new storage file:
$exeName -create example.properties -pass Example123Password!
echo.

REM Add properties
echo Adding properties:
$exeName -addUnsecured example.properties -key app@@name -value "Example Application"
$exeName -addSecured example.properties -key app@@secret -value "MySecretValue" -pass Example123Password!
echo.

REM Print all
echo Printing all properties:
$exeName -print example.properties
echo.

REM Get value
echo Getting encrypted value:
$exeName -getValue example.properties -key app@@secret -pass Example123Password!
echo.

echo ================================================
echo Example complete! Check example.properties
echo ================================================
pause
"@
        Set-Content -Path (Join-Path $packagePath "example.bat") -Value $exampleScript
    } else {
        # Create example script for Linux/macOS
        $exampleScript = @"
#!/bin/bash
# Example usage of SecuredPropertiesCSharp

echo "================================================"
echo " Secured Properties - Example Usage"
echo "================================================"
echo ""

# Generate a password
echo "Generating a random password:"
./$exeName -generatePassword
echo ""

# Create storage
echo "Creating a new storage file:"
./$exeName -create example.properties -pass Example123Password!
echo ""

# Add properties
echo "Adding properties:"
./$exeName -addUnsecured example.properties -key app@@name -value "Example Application"
./$exeName -addSecured example.properties -key app@@secret -value "MySecretValue" -pass Example123Password!
echo ""

# Print all
echo "Printing all properties:"
./$exeName -print example.properties
echo ""

# Get value
echo "Getting encrypted value:"
./$exeName -getValue example.properties -key app@@secret -pass Example123Password!
echo ""

echo "================================================"
echo "Example complete! Check example.properties"
echo "================================================"
"@
        Set-Content -Path (Join-Path $packagePath "example.sh") -Value $exampleScript
        
        # Make executable on Unix systems
        if ($RuntimeId.StartsWith('linux') -or $RuntimeId.StartsWith('osx')) {
            chmod +x (Join-Path $packagePath "example.sh") 2>$null
        }
    }
    
    # Create ZIP archive
    Write-Host "Creating ZIP archive..." -ForegroundColor Yellow
    $zipPath = "$packagePath.zip"
    if (Test-Path $zipPath) {
        Remove-Item -Force $zipPath
    }
    
    Compress-Archive -Path "$packagePath\*" -DestinationPath $zipPath -CompressionLevel Optimal
    
    $zipSize = (Get-Item $zipPath).Length
    $zipSizeMB = [math]::Round($zipSize / 1MB, 2)
    
    Write-Host "[OK] Package created: $packageName.zip ($zipSizeMB MB)" -ForegroundColor Green
    Write-Host "  Location: $zipPath" -ForegroundColor Gray
    Write-Host ""
    
    # Clean up unzipped folder
    Remove-Item -Recurse -Force $packagePath
}

# Publish for specified runtime(s)
if ($Runtime -eq 'all') {
    $runtimes = @('win-x64', 'linux-x64', 'osx-x64')
    
    foreach ($rt in $runtimes) {
        Publish-For-Runtime -RuntimeId $rt -BuildType $Type
    }
} else {
    Publish-For-Runtime -RuntimeId $Runtime -BuildType $Type
}

# Summary
Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "                    Publication Complete!                       " -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Distribution packages created in:" -ForegroundColor Green
Write-Host "  $outputPath" -ForegroundColor Yellow
Write-Host ""
Write-Host "Files ready for distribution:" -ForegroundColor Green
Get-ChildItem -Path $outputPath -Filter "*.zip" | ForEach-Object {
    $size = [math]::Round($_.Length / 1MB, 2)
    Write-Host "  • $($_.Name) ($size MB)" -ForegroundColor White
}
Write-Host ""
Write-Host "You can now copy these ZIP files to other machines!" -ForegroundColor Cyan
Write-Host ""
