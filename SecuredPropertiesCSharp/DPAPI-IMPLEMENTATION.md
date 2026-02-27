# Windows DPAPI Implementation

## Overview

The C# version of Secured Properties now implements Windows Data Protection API (DPAPI) encryption for the master password, matching the functionality of the Java version. This allows users on Windows to store their master password encrypted in the properties file itself, eliminating the need to enter it each time.

## How It Works

### Security Model

1. **Master Password Hash** (`STORAGE.MASTER_PASSWORD_HASH`)
   - Salted hash of the master password using PBKDF2-HMAC-SHA256
   - Used to verify the correctness of the entered password
   - 500,000 iterations for key derivation

2. **Windows DPAPI Encryption** (`STORAGE.MASTER_PASSWORD_WINDOWS_SECURED`)
   - Master password encrypted using Windows DPAPI (`ProtectedData.Protect`)
   - Tied to the current Windows user account
   - Only decryptable by the same user on the same machine
   - Uses `DataProtectionScope.CurrentUser`

3. **Verification Test** (`STORAGE.WINDOWS_SECURED`)
   - Contains an encrypted test string
   - Used to verify that DPAPI decryption works correctly
   - Ensures the file was encrypted by the current user

4. **Property Encryption**
   - User properties are encrypted using AES-256-CBC
   - Key derived from master password using PBKDF2
   - Each encrypted value includes: salt + IV + ciphertext

## File Format

```
-------------------------------@@HEADER_START@@-------------------------------------------------------------
STORAGE.MASTER_PASSWORD_HASH=<salt>$<hash>
STORAGE.MASTER_PASSWORD_WINDOWS_SECURED={ENC}<dpapi-encrypted-password>{ENC}
STORAGE.WINDOWS_SECURED={ENC}<encrypted-test-value>{ENC}
STORAGE.ENC_VERSION=2
-------------------------------@@HEADER_END@@-------------------------------------------------------------
myProperty=plaintext
mySecretProperty={ENC}<encrypted-value>{ENC}
...
```

## Usage

### Creating a Secured Storage

```bash
SecuredPropertiesCSharp.exe -create myStorage -pass "MyMasterPassword123"
```

This will:
- Create `myStorage.properties`
- Store master password hash
- Encrypt master password with Windows DPAPI (if on Windows)
- Store encrypted test value

### Adding Properties

```bash
# Add encrypted property
SecuredPropertiesCSharp.exe -addSecured myStorage.properties -key "apiKey" -value "secret123" -pass "MyMasterPassword123"

# Add plaintext property
SecuredPropertiesCSharp.exe -addUnsecured myStorage.properties -key "appName" -value "MyApp" -pass "MyMasterPassword123"
```

### Retrieving Properties

```bash
SecuredPropertiesCSharp.exe -getValue myStorage.properties -key "apiKey" -pass "MyMasterPassword123"
```

### Future Enhancement: Password-less Access

The current implementation includes the foundation for password-less access using DPAPI. A future enhancement could add:

```bash
# Open storage without password (DPAPI mode)
SecuredPropertiesCSharp.exe -getValue myStorage.properties -key "apiKey" -useWindows
```

This would use the `OpenSecuredStorageWithWindows()` method which:
1. Reads `STORAGE.MASTER_PASSWORD_WINDOWS_SECURED`
2. Decrypts it using Windows DPAPI (no password needed)
3. Uses the recovered master password to decrypt properties

## Implementation Details

### Key Methods

1. **`AddWindowsCheck(SecureString masterPassword)`**
   - Encrypts master password using `ProtectedData.Protect`
   - Stores encrypted password in `STORAGE.MASTER_PASSWORD_WINDOWS_SECURED`
   - Encrypts and stores test value in `STORAGE.WINDOWS_SECURED`

2. **`CheckWindowsSecurity()`**
   - Decrypts master password using `ProtectedData.Unprotect`
   - Verifies correctness by decrypting test value
   - Populates `_masterPassword` field for subsequent property decryption

3. **`OpenSecuredStorageWithWindows(string fileName)`**
   - Opens storage without requiring password input
   - Uses DPAPI to recover master password from file
   - Only works if file was encrypted by current Windows user

### Security Considerations

1. **User-Specific Encryption**
   - DPAPI-encrypted files are tied to the Windows user account
   - Cannot be decrypted by other users (even on same machine)
   - Cannot be decrypted on different machines

2. **Fallback Mode**
   - If DPAPI fails, falls back to master password mode
   - All operations continue to work with explicit password

3. **Memory Safety**
   - Password bytes are cleared from memory after encryption
   - Uses `Array.Clear()` to zero sensitive data

4. **Cross-Platform Compatibility**
   - DPAPI only available on Windows
   - Uses `OperatingSystem.IsWindows()` to check platform
   - Files remain compatible with password-based access on all platforms

## Comparison with Java Version

The C# implementation now matches the Java version's DPAPI functionality:

| Feature | Java (windpapi4j) | C# (ProtectedData) |
|---------|------------------|-------------------|
| Master password encryption | ✓ | ✓ |
| Test value verification | ✓ | ✓ |
| User-specific protection | ✓ | ✓ |
| Fallback to password mode | ✓ | ✓ |
| File format compatibility | ✓ | ✓ |

## NuGet Dependencies

The implementation requires:
```xml
<PackageReference Include="System.Security.Cryptography.ProtectedData" Version="10.0.0" />
```

This package provides Windows DPAPI access for .NET Core/.NET 5+.

## Testing

To verify DPAPI implementation:

1. Create a secured storage:
   ```bash
   SecuredPropertiesCSharp.exe -create test -pass "TestPassword123"
   ```

2. Verify the file contains:
   - `STORAGE.MASTER_PASSWORD_WINDOWS_SECURED`
   - `STORAGE.WINDOWS_SECURED`

3. Add and retrieve encrypted properties:
   ```bash
   SecuredPropertiesCSharp.exe -addSecured test.properties -key "secret" -value "myvalue" -pass "TestPassword123"
   SecuredPropertiesCSharp.exe -getValue test.properties -key "secret" -pass "TestPassword123"
   ```

## Limitations

1. **Windows Only**: DPAPI is only available on Windows systems
2. **User-Bound**: Files cannot be shared between users without the master password
3. **Machine-Bound**: Moving files to another machine requires master password for access
4. **Backup**: If Windows user profile is lost, recovery requires the master password

## Future Enhancements

1. Add `-useWindows` flag for password-less operations
2. Add command to check if file is Windows-secured: `-checkWindows`
3. Add command to remove Windows protection: `-removeWindows`
4. GUI integration for password-less mode
