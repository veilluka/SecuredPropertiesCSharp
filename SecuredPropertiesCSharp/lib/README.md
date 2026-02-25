# SecuredPropertiesCSharp Library

Version: 1.1.0

## Installation

### Option 1: Reference the DLL directly

1. Copy `SecuredPropertiesCSharp.dll` to your project
2. In Visual Studio: 
   - Right-click References → Add Reference
   - Browse to the DLL and add it
3. In your code: `using SecuredPropertiesCSharp;`

### Option 2: Add via project file (.csproj)

Add this to your .csproj file:
```xml
<ItemGroup>
  <Reference Include="SecuredPropertiesCSharp">
    <HintPath>path\to\SecuredPropertiesCSharp.dll</HintPath>
  </Reference>
</ItemGroup>
```

### Option 3: Install NuGet package (if created)

```bash
dotnet add package SecuredPropertiesCSharp --source ./lib
```

## Quick Start

```csharp
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
```

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

See `USAGE-EXAMPLE.cs` for complete examples.

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
