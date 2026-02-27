# Quick Start - Creating a Standalone Executable

## Simplest Method (Recommended)

### For Distribution to Other Machines:
```powershell
cd C:\data\dev\github\secured-properties\vs\SecuredPropertiesCSharp
.\publish.ps1
```

This creates a complete package at `dist\SecuredPropertiesCSharp-1.0.0-win-x64.zip` with:
- Standalone executable
- README with examples
- Example batch script

**Just copy the ZIP file to another machine, extract, and run!**

### For Development/Testing:
```powershell
cd C:\data\dev\github\secured-properties\vs\SecuredPropertiesCSharp
.\build-exe.ps1
```

The executable will be created at:
```
bin\Release\net10.0\win-x64\publish\SecuredPropertiesCSharp.exe
```

### Manual Command:
```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```

## Using the Executable

Copy `SecuredPropertiesCSharp.exe` anywhere and run:

```bash
# Show help
SecuredPropertiesCSharp.exe -help

# Generate a password
SecuredPropertiesCSharp.exe -generatePassword

# Create storage
SecuredPropertiesCSharp.exe -create myconfig.properties -pass MyPassword123!

# Add encrypted property
SecuredPropertiesCSharp.exe -addSecured myconfig.properties -key app.api.key -value secret123 -pass MyPassword123!

# Get value
SecuredPropertiesCSharp.exe -getValue myconfig.properties -key app.api.key -pass MyPassword123!

# Print all
SecuredPropertiesCSharp.exe -print myconfig.properties
```

## File Size

- **Self-contained**: ~70-75 MB (includes .NET runtime, runs anywhere)
- **Trimmed**: ~40-50 MB (removes unused code)
- **Framework-dependent**: ~200 KB (requires .NET 8.0+ installed)

## Different Build Types

```powershell
# Self-contained (no .NET required on target PC)
.\build-exe.ps1 -Type self-contained

# Trimmed (smaller size)
.\build-exe.ps1 -Type trimmed

# Framework-dependent (tiny but needs .NET installed)
.\build-exe.ps1 -Type framework-dependent
```

## Build for Other Platforms

```powershell
# Linux
.\build-exe.ps1 -Runtime linux-x64

# macOS Intel
.\build-exe.ps1 -Runtime osx-x64

# macOS Apple Silicon
.\build-exe.ps1 -Runtime osx-arm64

# All platforms at once
.\build-exe.ps1 -Runtime all
```

## What You Need

- .NET 8.0 SDK or later installed (for building)
- That's it!

The resulting executable can run on any PC **without** .NET installed (if using self-contained build).

## More Info

- Full deployment guide: [DEPLOYMENT.md](DEPLOYMENT.md)
- Main README: [README.md](README.md)
