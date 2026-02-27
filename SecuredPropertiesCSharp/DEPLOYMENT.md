# Deployment Guide - Creating Standalone Executables

This guide explains how to create a standalone executable from the SecuredPropertiesCSharp project.

## Option 1: Single-File Self-Contained (Recommended)

This creates a single EXE file that includes the .NET runtime and can run on any Windows PC without requiring .NET to be installed.

### For Windows x64:
```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```

**Output:** `bin\Release\net10.0\win-x64\publish\SecuredPropertiesCSharp.exe` (~70-75 MB)

### For Windows ARM64:
```bash
dotnet publish -c Release -r win-arm64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```

### For Linux x64:
```bash
dotnet publish -c Release -r linux-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```

### For macOS (Intel):
```bash
dotnet publish -c Release -r osx-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```

### For macOS (Apple Silicon):
```bash
dotnet publish -c Release -r osx-arm64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```

## Option 2: Framework-Dependent (Smaller Size)

This creates a smaller executable but requires .NET 8.0 or later to be installed on the target PC.

```bash
dotnet publish -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true
```

**Output:** `bin\Release\net10.0\win-x64\publish\SecuredPropertiesCSharp.exe` (~200 KB)

**Requirement:** User must have .NET 8.0 Runtime installed from https://dotnet.microsoft.com/download

## Option 3: Trimmed Self-Contained (Smallest Self-Contained)

This removes unused code to create a smaller self-contained executable.

```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=true /p:TrimMode=partial
```

**Output:** Smaller than Option 1 (~40-50 MB)

**Note:** Trimming may remove code that's only used via reflection. Test thoroughly.

## Option 4: ReadyToRun (Faster Startup)

This pre-compiles the code for faster startup at the cost of larger file size.

```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=true
```

## Using the Published Executable

After publishing, you can find the executable at:
```
bin\Release\net10.0\{runtime-identifier}\publish\SecuredPropertiesCSharp.exe
```

### Examples:
```bash
# Show help
SecuredPropertiesCSharp.exe -help

# Generate password
SecuredPropertiesCSharp.exe -generatePassword

# Create storage
SecuredPropertiesCSharp.exe -create config.properties -pass MyPassword123!

# Add property
SecuredPropertiesCSharp.exe -addSecured config.properties -key api.key -value secret -pass MyPassword123!
```

## Distribution

### Single Executable (Option 1)
Simply copy the `.exe` file from the publish folder and distribute it. No installation required.

### Framework-Dependent (Option 2)
1. Distribute the `.exe` file
2. Instruct users to install .NET 8.0 Runtime from: https://dotnet.microsoft.com/download/dotnet/8.0

## Advanced Options

### Minimize File Size Further
Add these to your `.csproj` file:

```xml
<PropertyGroup>
  <PublishSingleFile>true</PublishSingleFile>
  <SelfContained>true</SelfContained>
  <PublishTrimmed>true</PublishTrimmed>
  <TrimMode>partial</TrimMode>
  <DebuggerSupport>false</DebuggerSupport>
  <EnableUnsafeBinaryFormatterSerialization>false</EnableUnsafeBinaryFormatterSerialization>
  <InvariantGlobalization>true</InvariantGlobalization>
</PropertyGroup>
```

Then publish:
```bash
dotnet publish -c Release -r win-x64
```

### Create Multiple Platform Builds
Create a script to build for all platforms:

**build-all.ps1:**
```powershell
$runtimes = @("win-x64", "win-arm64", "linux-x64", "osx-x64", "osx-arm64")

foreach ($runtime in $runtimes) {
    Write-Host "Building for $runtime..." -ForegroundColor Green
    dotnet publish -c Release -r $runtime --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
}

Write-Host "Build complete! Executables are in bin\Release\net10.0\{runtime}\publish\" -ForegroundColor Green
```

## Recommended Approach

**For distribution to users without .NET installed:**
- Use Option 1 (Single-File Self-Contained)
- File is larger but requires no dependencies
- Works immediately on any Windows PC

**For internal use where .NET is already installed:**
- Use Option 2 (Framework-Dependent)
- Much smaller file size
- Faster to distribute

**For production deployment:**
- Use Option 4 (ReadyToRun) for best startup performance
- Test Option 3 (Trimmed) if file size is critical

## File Locations After Publishing

```
SecuredPropertiesCSharp/
└── bin/
    └── Release/
        └── net10.0/
            └── win-x64/          # or other runtime identifier
                └── publish/
                    ├── SecuredPropertiesCSharp.exe  ← This is your distributable
                    └── SecuredPropertiesCSharp.pdb  (optional, for debugging)
```

The `.pdb` file is for debugging and doesn't need to be distributed.

## Troubleshooting

### "The application requires a higher version of .NET"
- You're using Option 2 and .NET isn't installed on the target PC
- Solution: Use Option 1 or install .NET 8.0+ on target PC

### "File is too large"
- Try Option 3 (Trimmed) or Option 2 (Framework-Dependent)
- Remove the `.pdb` file (it's only for debugging)

### "Application won't start"
- Make sure you're using the correct runtime identifier for your target OS
- For Windows, use `win-x64` (most common) or `win-arm64` (ARM devices)
- Ensure the executable has execute permissions (especially on Linux/macOS)

## Runtime Identifiers (RIDs)

Common RIDs:
- `win-x64` - Windows 64-bit (most common)
- `win-x86` - Windows 32-bit
- `win-arm64` - Windows ARM64 (Surface Pro X, etc.)
- `linux-x64` - Linux 64-bit
- `linux-arm64` - Linux ARM64 (Raspberry Pi, etc.)
- `osx-x64` - macOS Intel
- `osx-arm64` - macOS Apple Silicon (M1/M2/M3)

Full list: https://learn.microsoft.com/en-us/dotnet/core/rid-catalog
