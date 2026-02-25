#!/usr/bin/env pwsh
# Build script for creating NuGet package and class library DLL

param(
    [Parameter(Mandatory=$false)]
    [string]$OutputFolder = "lib",
    
    [Parameter(Mandatory=$false)]
    [string]$Configuration = "Release",
    
    [Parameter(Mandatory=$false)]
    [switch]$CreateNuGet
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "     SecuredPropertiesCSharp - Library Builder                 " -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host ""

# Get version
$version = "1.0.0"
if (Test-Path "SecuredPropertiesCSharp.csproj") {
    $csproj = Get-Content "SecuredPropertiesCSharp.csproj" -Raw
    if ($csproj -match '<Version>(.*?)</Version>') {
        $version = $matches[1]
    }
}

Write-Host "Building library version: $version" -ForegroundColor Yellow
Write-Host ""

# Create output directory
$outputPath = Join-Path $PSScriptRoot $OutputFolder
if (!(Test-Path $outputPath)) {
    New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
}

# Clean previous builds
Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
try {
    if (Test-Path "bin") { Remove-Item -Recurse -Force "bin" -ErrorAction SilentlyContinue }
    if (Test-Path "obj") { Remove-Item -Recurse -Force "obj" -ErrorAction SilentlyContinue }
} catch {
    Write-Host "  Some files are locked, continuing..." -ForegroundColor Gray
}

# Build the library
Write-Host "Building library..." -ForegroundColor Green
dotnet build -c $Configuration

if ($LASTEXITCODE -ne 0) {
    Write-Host "[FAIL] Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "[OK] Build successful!" -ForegroundColor Green
Write-Host ""

# Copy DLL to output folder
Write-Host "Copying library files..." -ForegroundColor Yellow

$dllPath = "bin\$Configuration\net10.0\SecuredPropertiesCSharp.dll"
$xmlPath = "bin\$Configuration\net10.0\SecuredPropertiesCSharp.xml"

if (Test-Path $dllPath) {
    Copy-Item $dllPath -Destination $outputPath -Force
    Write-Host "[OK] Copied: SecuredPropertiesCSharp.dll" -ForegroundColor Green
    
    $dllSize = (Get-Item $dllPath).Length
    $dllSizeKB = [math]::Round($dllSize / 1KB, 2)
    Write-Host "  Size: $dllSizeKB KB" -ForegroundColor Gray
} else {
    Write-Host "[FAIL] DLL not found!" -ForegroundColor Red
    exit 1
}

# Copy XML documentation if exists
if (Test-Path $xmlPath) {
    Copy-Item $xmlPath -Destination $outputPath -Force
    Write-Host "[OK] Copied: SecuredPropertiesCSharp.xml (IntelliSense documentation)" -ForegroundColor Green
}

Write-Host ""

# Create NuGet package if requested
if ($CreateNuGet) {
    Write-Host "Creating NuGet package..." -ForegroundColor Yellow
    dotnet pack -c $Configuration -o $outputPath
    
    if ($LASTEXITCODE -eq 0) {
        $nupkgFile = Get-ChildItem -Path $outputPath -Filter "*.nupkg" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($nupkgFile) {
            $nupkgSize = [math]::Round($nupkgFile.Length / 1KB, 2)
            Write-Host "[OK] NuGet package created: $($nupkgFile.Name) ($nupkgSize KB)" -ForegroundColor Green
        }
    } else {
        Write-Host "[FAIL] NuGet package creation failed!" -ForegroundColor Red
    }
    Write-Host ""
}

# Create sample integration project
Write-Host "Creating usage example..." -ForegroundColor Yellow

$exampleContent = @"
using SecuredPropertiesCSharp;

// Example 1: Basic encryption/decryption
var enc = new Enc();
var encrypted = enc.Encrypt("Hello, World!", "MyPassword123!");
var decrypted = enc.Decrypt(encrypted, "MyPassword123!");
Console.WriteLine(`$"Encrypted: {encrypted}");
Console.WriteLine(`$"Decrypted: {decrypted}");

// Example 2: Password generation
var password = enc.GeneratePassword(lowerCase: 6, upperCase: 8, numbers: 10, symbols: 6);
Console.WriteLine(`$"Generated password: {password}");

// Example 3: SecureStorage
var masterPassword = new SecureString("YourSecurePassword123!");
SecStorage.CreateNewSecureStorage("myconfig.properties", masterPassword, createSecured: true);

var storage = SecStorage.OpenSecuredStorage("myconfig.properties", masterPassword);
storage.AddUnsecuredProperty("app@@name", "MyApp");
storage.AddSecuredProperty("app@@api@@key", new SecureString("secret-key-123"));

var apiKey = storage.GetPropertyValue("app@@api@@key");
Console.WriteLine(`$"API Key: {apiKey}");

SecStorage.Destroy();

// Example 4: SecureProperties (lower level)
var props = SecureProperties.CreateSecuredProperties("test.properties");
props.AddStringProperty(new[] { "database", "host" }, "localhost", encrypted: false);
props.AddStringProperty(new[] { "database", "password" }, "dbpass123", encrypted: true);
props.SaveProperties();

// Example 5: Password hashing
var hashedPassword = enc.GetSaltedHash("mypassword".ToCharArray());
Console.WriteLine(`$"Hashed: {hashedPassword}");

bool isValid = enc.Check("mypassword", hashedPassword);
Console.WriteLine(`$"Password valid: {isValid}");
"@

$examplePath = Join-Path $outputPath "USAGE-EXAMPLE.cs"
Set-Content -Path $examplePath -Value $exampleContent
Write-Host "[OK] Created: USAGE-EXAMPLE.cs" -ForegroundColor Green

# Create README for library users
$readmeContent = @"
# SecuredPropertiesCSharp Library

Version: $version

## Installation

### Option 1: Reference the DLL directly

1. Copy ``SecuredPropertiesCSharp.dll`` to your project
2. In Visual Studio: 
   - Right-click References → Add Reference
   - Browse to the DLL and add it
3. In your code: ``using SecuredPropertiesCSharp;``

### Option 2: Add via project file (.csproj)

Add this to your .csproj file:
``````xml
<ItemGroup>
  <Reference Include="SecuredPropertiesCSharp">
    <HintPath>path\to\SecuredPropertiesCSharp.dll</HintPath>
  </Reference>
</ItemGroup>
``````

### Option 3: Install NuGet package (if created)

``````bash
dotnet add package SecuredPropertiesCSharp --source ./lib
``````

## Quick Start

``````csharp
using SecuredPropertiesCSharp;

// Encrypt/Decrypt
var enc = new Enc();
var encrypted = enc.Encrypt("secret data", "password");
var decrypted = enc.Decrypt(encrypted, "password");

// Secure Storage
var masterPass = new SecureString("MasterPassword123!");
SecStorage.CreateNewSecureStorage("config.properties", masterPass, true);

var storage = SecStorage.OpenSecuredStorage("config.properties", masterPass);
storage.AddSecuredProperty("api@@key", new SecureString("my-secret-key"));
var apiKey = storage.GetPropertyValue("api@@key");

SecStorage.Destroy();
``````

## Main Classes

### Enc
- **Encrypt/Decrypt**: AES-256-CBC encryption
- **Password Hashing**: PBKDF2 with HMAC-SHA256
- **Password Generation**: Secure random passwords

### SecStorage
- **High-level API**: Master password-protected storage
- **Automatic encryption**: Secured properties are auto-encrypted
- **Easy CRUD**: Add, get, delete properties

### SecureProperties
- **Mid-level API**: Property collection management
- **File I/O**: Read/write .properties files
- **Hierarchical keys**: Support for @@ separator

### SecureProperty
- **Low-level API**: Individual property management
- **Key management**: Hierarchical key support

### SecureString
- **Secure handling**: Explicit memory cleanup
- **Password storage**: Safe string wrapper

## Code Examples

See ``USAGE-EXAMPLE.cs`` for complete examples.

## Requirements

- .NET 8.0 or later
- Target Framework: net10.0

## Documentation

The DLL includes XML documentation for IntelliSense support.

## Features

- AES-256-CBC encryption
- PBKDF2 password hashing (500,000 iterations)
- Master password protection
- Hierarchical property keys
- Mix encrypted/unencrypted properties
- Secure memory handling
- No external dependencies

## License

See main project repository for license information.

---
Built with .NET 8.0
"@

$readmePath = Join-Path $outputPath "README.md"
Set-Content -Path $readmePath -Value $readmeContent
Write-Host "[OK] Created: README.md" -ForegroundColor Green

Write-Host ""
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "                Library Build Complete!                        " -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Library files created in:" -ForegroundColor Green
Write-Host "  $outputPath" -ForegroundColor Yellow
Write-Host ""
Write-Host "Files:" -ForegroundColor Green
Get-ChildItem -Path $outputPath -File | ForEach-Object {
    $size = [math]::Round($_.Length / 1KB, 2)
    Write-Host "  - $($_.Name) ($size KB)" -ForegroundColor White
}
Write-Host ""
Write-Host "To use in your project:" -ForegroundColor Cyan
Write-Host "  1. Add reference to SecuredPropertiesCSharp.dll" -ForegroundColor White
Write-Host "  2. Add: using SecuredPropertiesCSharp;" -ForegroundColor White
Write-Host "  3. See USAGE-EXAMPLE.cs for code examples" -ForegroundColor White
Write-Host ""

if (!$CreateNuGet) {
    Write-Host "Tip: Run with -CreateNuGet to also create a NuGet package" -ForegroundColor Gray
    Write-Host ""
}
