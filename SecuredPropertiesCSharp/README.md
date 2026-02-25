# Secured Properties (C#)

A C# implementation of encryption utilities for password hashing, encryption, and decryption using AES and PBKDF2.

## Features

- **Password Hashing**: Salted password hashing using PBKDF2 with HMAC-SHA256
- **AES Encryption/Decryption**: AES-256 encryption in CBC mode with PKCS7 padding
- **Password Generation**: Secure random password generation with customizable character sets
- **Password Verification**: Verify passwords against stored salted hashes
- **Secure Property Management**: Hierarchical key-value storage with encryption support
- **Properties File I/O**: Read and write properties files with optional encryption markers
- **Secure Storage**: Master password-protected storage with automatic encryption/decryption
- **Secure String Handling**: Secure string wrapper with explicit memory cleanup

## Usage

### Build and Run

```bash
dotnet build
dotnet run
```

### Command Line Interface

The application can be used as a command-line tool for managing secure properties:

```bash
# Show help
dotnet run -- -help

# Create secured storage
dotnet run -- -create myconfig.properties -pass MySecurePass123!

# Add encrypted property
dotnet run -- -addSecured myconfig.properties -key app@@api@@key -value secret123 -pass MySecurePass123!

# Add unencrypted property
dotnet run -- -addUnsecured myconfig.properties -key app@@name -value MyApp

# Get property value
dotnet run -- -getValue myconfig.properties -key app@@api@@key -pass MySecurePass123!

# Print all properties
dotnet run -- -print myconfig.properties

# Delete property
dotnet run -- -delete myconfig.properties -key app@@api@@key -pass MySecurePass123!

# Generate random password
dotnet run -- -generatePassword
```

### Code Examples

#### Hash a Password
```csharp
var enc = new Enc();
string hashedPassword = enc.GetSaltedHash("myPassword".ToCharArray());
```

#### Verify a Password
```csharp
var enc = new Enc();
bool isValid = enc.Check("myPassword", storedHash);
```

#### Encrypt Text
```csharp
var enc = new Enc();
string encrypted = enc.Encrypt("Secret message", "encryptionKey");
```

#### Decrypt Text
```csharp
var enc = new Enc();
string decrypted = enc.Decrypt(encryptedText, "encryptionKey");
```

#### Generate Random Password
```csharp
var enc = new Enc();
string password = enc.GeneratePassword(lowerCase: 6, upperCase: 8, numbers: 10, symbols: 6);
```

### SecureProperty

Manage hierarchical keys with encryption flags.

#### Create a Property
```csharp
var prop = SecureProperty.CreateNewSecureProperty(
    "app@@database@@host", 
    "localhost", 
    encrypted: false);
```

#### Work with Keys
```csharp
var key = SecureProperty.CreateKey("app@@database@@host");
var keyString = SecureProperty.CreateKeyWithSeparator(key);
var parts = SecureProperty.ParseKey("app@@database@@host");
```

### SecureProperties

Store and manage collections of secure properties.

#### Create and Save Properties
```csharp
var props = SecureProperties.CreateSecuredProperties("config.properties");
props.AddStringProperty(new[] { "app", "database", "host" }, "localhost", false);
props.AddStringProperty(new[] { "app", "database", "password" }, "secret", true);
props.AddBooleanProperty(new[] { "app", "ssl", "enabled" }, true);
props.SaveProperties();
```

#### Load and Read Properties
```csharp
var props = SecureProperties.OpenSecuredProperties("config.properties");
string? host = props.GetStringProperty("app@@database@@host");
bool ssl = props.GetBooleanValue("app@@ssl@@enabled");

// Get all keys under a parent
var dbProps = props.GetAllProperties("app@@database");

// Get all labels
var labels = props.GetAllLabels();
```

### SecureString

Securely handle sensitive strings with explicit memory cleanup.

```csharp
var secureStr = new SecureString("sensitive-data");

// Use the value
char[] value = secureStr.Value;
string str = secureStr.ToString();

// Explicitly destroy when done
secureStr.DestroyValue();
```

### SecStorage

High-level secure storage with master password protection.

#### Create Secured Storage
```csharp
var masterPassword = new SecureString("YourSecurePassword123!");
SecStorage.CreateNewSecureStorage("config.properties", masterPassword, createSecured: true);
```

#### Open and Use Storage
```csharp
var masterPassword = new SecureString("YourSecurePassword123!");
var storage = SecStorage.OpenSecuredStorage("config.properties", masterPassword);

// Add unsecured property
storage.AddUnsecuredProperty("app@@name", "MyApp");

// Add secured property (automatically encrypted)
storage.AddSecuredProperty("app@@api@@key", new SecureString("secret-key"));

// Retrieve properties (automatically decrypted if encrypted)
string appName = storage.GetPropertyStringValue("app@@name");
SecureString apiKey = storage.GetPropertyValue("app@@api@@key");

// List all keys
var allKeys = storage.GetAllKeys();

// Check if file is secured
bool isSecured = SecStorage.IsSecured("config.properties");

// Verify password
bool isCorrect = SecStorage.IsPasswordCorrect("config.properties", masterPassword);

// Change master password
SecStorage.ChangeMasterPassword("config.properties", currentPassword, newPassword);

// Clean up
SecStorage.Destroy();
```

## Security Details

- **Algorithm**: AES-256-CBC
- **Key Derivation**: PBKDF2-HMAC-SHA256
- **Iterations**: 500,000
- **Salt Length**: 64 bytes
- **IV**: Randomly generated for each encryption (16 bytes)

## Requirements

- .NET 8.0 SDK or later

## Project Structure

- `Enc.cs` - Main encryption class with all cryptographic functions
- `SecureString.cs` - Secure string wrapper for sensitive data with explicit cleanup
- `SecureProperty.cs` - Property class with hierarchical key support and LinkedHashSet implementation
- `SecureProperties.cs` - Property collection management with file I/O
- `SecStorage.cs` - High-level secure storage with master password protection
- `ConsoleParser.cs` - Command-line argument parser with improved help system
- `ConsoleApp.cs` - Command-line interface for managing secure properties
- `Program.cs` - Entry point that supports both CLI and demo modes

## Deployment

See [DEPLOYMENT.md](DEPLOYMENT.md) for detailed instructions on creating standalone executables.

### Quick Start - Create Executable

**Windows (self-contained, no .NET required):**
```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```

**Or use the build script:**
```bash
.\build-exe.ps1                  # Build for Windows x64
.\build-exe.ps1 -Runtime all     # Build for all platforms
.\build-exe.ps1 -Type trimmed    # Build smaller version
```

**Output:** `bin\Release\net10.0\win-x64\publish\SecuredPropertiesCSharp.exe`

The executable can be copied and run on any Windows PC without installing .NET.

## Ported From

This is a C# port of the Kotlin implementation from the `secured-properties` project.
