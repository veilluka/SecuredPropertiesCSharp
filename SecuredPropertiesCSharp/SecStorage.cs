using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace SecuredPropertiesCSharp
{
    public class SecStorage
    {
        // Storage constants
        public const string TEST_STRING = "STORAGE.WINDOWS_SECURED";
        public const string MASTER_PASSWORD_WIN_SECURED = "STORAGE.MASTER_PASSWORD_WINDOWS_SECURED";
        public const string MASTER_PASSWORD_HASH = "STORAGE.MASTER_PASSWORD_HASH";
        public const string ENC_VERSION = "STORAGE.ENC_VERSION";

        // Password file constants
        public const string PASSWORD_FILE_NAME = "master_password_plain_text_store_and_delete.txt";
        public const string MASTER_PASSWORD_KEY = "MASTER_PASSWORD";

        // Exception messages
        public const string PASSWORD_TOO_SHORT = "Password must be at least 12 characters";
        public const string NOT_WINDOWS_SUPPORTED = "Windows DPAPI is not supported";
        public const string MASTER_KEY_NOT_SET = "Master key is not set";
        public const string PASSWORD_NOT_CORRECT = "Password is not correct";
        public const string NO_MASTER_KEY = "No master key found";
        public const string SECURE_MODE_NOT_ON = "Secure mode is not enabled";

        private static SecStorage? _storage = null;
        private SecureProperties? _secureProperties = null;
        private SecureString? _masterPassword = null;
        private bool _secureMode = false;

        public bool IsSecureMode => _secureMode;

        /// <summary>
        /// Destroy the current storage instance and clear sensitive data
        /// </summary>
        public static void Destroy()
        {
            if (_storage?._masterPassword != null)
                _storage._masterPassword.DestroyValue();

            if (_storage != null)
            {
                _storage._secureMode = false;
                _storage = null;
            }
        }

        /// <summary>
        /// Create a new secure storage file
        /// </summary>
        public static void CreateNewSecureStorage(string fileName, SecureString? masterPassword, bool createSecured)
        {
            if (createSecured && (masterPassword == null || masterPassword.Value == null || masterPassword.Value.Length < 12))
            {
                throw new SecureStorageException(PASSWORD_TOO_SHORT);
            }

            _storage = new SecStorage();
            _storage._secureProperties = SecureProperties.CreateSecuredProperties(fileName);
            _storage.AddUnsecuredProperty(ENC_VERSION, "2");

            if (!createSecured) return;

            var saltedHash = new Enc().GetSaltedHash(masterPassword!.Value!);
            _storage.AddUnsecuredProperty(MASTER_PASSWORD_HASH, saltedHash);

            // Add Windows DPAPI encryption if supported
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                try
                {
                    _storage.AddWindowsCheck(masterPassword);
                }
                catch (Exception)
                {
                    // DPAPI failed, continue without it
                }
            }

            SecureEntries();
        }

        /// <summary>
        /// Check if a file is secured with a password
        /// </summary>
        public static bool IsSecured(string fileName)
        {
            _storage = new SecStorage();
            _storage._secureProperties = SecureProperties.OpenSecuredProperties(fileName, checkVersion: true);
            return _storage.HasProperty(MASTER_PASSWORD_HASH);
        }

        /// <summary>
        /// Open a secured storage with master password
        /// </summary>
        public static SecStorage OpenSecuredStorage(string fileName, SecureString masterPassword)
        {
            Log.Info($"OpenSecuredStorage(file='{fileName}', with explicit password)");
            if (masterPassword == null)
            {
                throw new SecureStorageException(MASTER_KEY_NOT_SET);
            }

            _storage = new SecStorage();
            _storage._secureProperties = SecureProperties.OpenSecuredProperties(fileName, checkVersion: true);

            Log.Info("Checking master key hash...");
            if (!_storage.CheckMasterKey(masterPassword))
            {
                Log.Info("Master key check FAILED");
                throw new SecureStorageException(PASSWORD_NOT_CORRECT);
            }
            Log.Info("Master key check OK");

            // Try to use Windows DPAPI if available
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                try
                {
                    Log.Info("Attempting AddWindowsCheck (DPAPI re-encrypt)...");
                    _storage.AddWindowsCheck(masterPassword);
                    _storage._secureMode = true;
                    Log.Info($"AddWindowsCheck succeeded. secureMode={_storage._secureMode}, masterPassword set={_storage._masterPassword != null}");
                }
                catch (Exception ex)
                {
                    // Fall back to master password mode
                    Log.Info($"AddWindowsCheck failed: {ex.GetType().Name}: {ex.Message}. Falling back to password mode.");
                    _storage._secureMode = true;
                    _storage._masterPassword = masterPassword;
                }
            }
            else
            {
                _storage._secureMode = true;
                _storage._masterPassword = masterPassword;
            }

            Log.Info($"OpenSecuredStorage with password done. secureMode={_storage._secureMode}, masterPassword set={_storage._masterPassword != null}");
            return _storage;
        }

        /// <summary>
        /// Open storage, attempting Windows DPAPI decryption when secured.
        /// Falls back to companion password file or MASTER_PASSWORD=XXX in properties file.
        /// </summary>
        public static SecStorage OpenSecuredStorage(string fileName, bool openSecured)
        {
            Log.Info($"OpenSecuredStorage(file='{fileName}', openSecured={openSecured})");
            _storage = new SecStorage();
            _storage._secureProperties = SecureProperties.OpenSecuredProperties(fileName, checkVersion: true);

            if (!openSecured)
            {
                Log.Info("openSecured=false, returning unsecured storage");
                return _storage;
            }

            // Try Windows DPAPI first if the file has a DPAPI-encrypted master password
            var isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
            var hasDpapiProp = _storage.HasProperty(MASTER_PASSWORD_WIN_SECURED);
            Log.Info($"IsWindows={isWindows}, HasDPAPI_Property={hasDpapiProp}");

            if (isWindows && hasDpapiProp)
            {
                try
                {
                    Log.Info("Attempting DPAPI CheckWindowsSecurity()...");
                    _storage.CheckWindowsSecurity();
                    _storage._secureMode = true;
                    Log.Info($"DPAPI succeeded. secureMode={_storage._secureMode}, masterPassword set={_storage._masterPassword != null}");
                    return _storage;
                }
                catch (Exception ex)
                {
                    Log.Info($"DPAPI failed: {ex.GetType().Name}: {ex.Message}");
                }
            }

            var hashProp = _storage.GetProperty(MASTER_PASSWORD_HASH);
            Log.Info($"MASTER_PASSWORD_HASH property exists={hashProp != null}, encrypted={hashProp?.IsEncrypted}");
            var hash = _storage.GetPropertyValue(MASTER_PASSWORD_HASH);
            Log.Info($"MASTER_PASSWORD_HASH value null={hash == null || hash.Value == null}");
            if (hash == null || hash.Value == null)
            {
                throw new SecureStorageException(MASTER_KEY_NOT_SET);
            }

            // DPAPI not available or failed — try to recover master password from other sources
            string? masterPasswordStr = null;
            bool foundInPropertiesFile = false;

            // 1. Try password from companion txt file
            var passwordFilePath = GetPasswordFilePath(fileName);
            Log.Info($"Checking companion password file: {passwordFilePath}");
            masterPasswordStr = ReadPasswordFromFile(fileName);
            Log.Info($"Companion file password found={masterPasswordStr != null}");

            // 2. Try MASTER_PASSWORD from properties file itself
            if (masterPasswordStr == null)
            {
                Log.Info("Checking MASTER_PASSWORD entry in properties file...");
                masterPasswordStr = ReadMasterPasswordFromProperties(fileName);
                if (masterPasswordStr != null)
                {
                    foundInPropertiesFile = true;
                    Log.Info("Found MASTER_PASSWORD in properties file");
                }
                else
                {
                    Log.Info("MASTER_PASSWORD not found in properties file");
                }
            }

            if (masterPasswordStr == null)
            {
                Log.Info("No password source available — throwing exception");
                throw new SecureStorageException(
                    "Cannot decrypt properties. DPAPI failed (different user/machine). " +
                    "Provide the master password using -pass option, or add MASTER_PASSWORD=XXX as plain text to the properties file and retry.");
            }

            Log.Info("Opening storage with recovered password...");
            var securePass = new SecureString(masterPasswordStr);
            SecStorage secStorage;

            try
            {
                secStorage = OpenSecuredStorage(fileName, securePass);
            }
            catch (SecureStorageException ex) when (
                ex.Message == PASSWORD_NOT_CORRECT ||
                ex.Message == MASTER_KEY_NOT_SET)
            {
                Log.Info($"Password recovery open failed: {ex.Message}. Reconstructing hash...");
                _storage = new SecStorage();
                _storage._secureProperties = SecureProperties.OpenSecuredProperties(fileName, checkVersion: false);

                var saltedHash = new Enc().GetSaltedHash(new SecureString(masterPasswordStr).Value!);
                _storage.AddUnsecuredProperty(MASTER_PASSWORD_HASH, saltedHash);

                if (!_storage.HasProperty(ENC_VERSION))
                    _storage.AddUnsecuredProperty(ENC_VERSION, "2");

                secStorage = OpenSecuredStorage(fileName, new SecureString(masterPasswordStr));
            }

            // Remove MASTER_PASSWORD plain text from properties file if it was there
            if (foundInPropertiesFile)
            {
                secStorage.DeleteProperty(MASTER_PASSWORD_KEY);
                Log.Info("Removed MASTER_PASSWORD plain text from properties file");
            }

            Log.Info($"Storage opened via password recovery. secureMode={secStorage.IsSecureMode}");
            return secStorage;
        }

        private static void SecureEntries()
        {
            if (_storage?._secureProperties == null) return;

            var secureEntries = new List<SecureProperty>();

            foreach (var entryKey in _storage._secureProperties._map.Keys)
            {
                foreach (var entry in _storage._secureProperties._map[entryKey])
                {
                    if (entry.Value != null && entry.Value.StartsWith("{ENC}"))
                    {
                        secureEntries.Add(entry);
                    }
                }
            }

            foreach (var secureProperty in secureEntries)
            {
                secureProperty.Value = secureProperty.Value?.Replace("{ENC}", "");
                _storage.Secure(secureProperty);
            }
        }

        /// <summary>
        /// Check if the provided password is correct
        /// </summary>
        public static bool IsPasswordCorrect(string fileName, SecureString masterPassword)
        {
            _storage = new SecStorage();
            _storage._secureProperties = SecureProperties.OpenSecuredProperties(fileName, checkVersion: true);

            if (!_storage.CheckMasterKey(masterPassword))
            {
                Destroy();
                return false;
            }

            Destroy();
            return true;
        }

        /// <summary>
        /// Change the master password
        /// </summary>
        public static void ChangeMasterPassword(string fileName, SecureString? currentPassword, SecureString newPassword)
        {
            if (!File.Exists(fileName))
                throw new SecureStorageException(SecureStorageException.FILE_NOT_EXISTS + fileName);

            _storage = new SecStorage();
            _storage._secureProperties = SecureProperties.OpenSecuredProperties(fileName, checkVersion: true);

            if (!_storage.IsSecureMode && currentPassword == null)
            {
                var saltedHash = new Enc().GetSaltedHash(newPassword.Value!);
                _storage.AddUnsecuredProperty(MASTER_PASSWORD_HASH, saltedHash);
                return;
            }

            if (currentPassword != null && !_storage.CheckMasterKey(currentPassword))
            {
                throw new SecureStorageException(PASSWORD_NOT_CORRECT);
            }

            var newSaltedHash = new Enc().GetSaltedHash(newPassword.Value!);
            _storage.AddUnsecuredProperty(MASTER_PASSWORD_HASH, newSaltedHash);
        }

        private bool CheckMasterKey(SecureString masterPassword)
        {
            var masterKeyHash = GetPropertyValue(MASTER_PASSWORD_HASH);

            if (masterKeyHash?.ToString() != null)
            {
                try
                {
                    return new Enc().Check(masterPassword.ToString()!, masterKeyHash.ToString()!);
                }
                catch (Exception)
                {
                    return false;
                }
            }

            return false;
        }

        private void AddProperty(SecureProperty prop)
        {
            _secureProperties?.AddProperty(prop);
            _secureProperties?.SaveProperties();
        }

        public List<string> GetAllKeys()
        {
            return _secureProperties?.GetAllKeys() ?? new List<string>();
        }

        public List<SecureProperty> GetAllProperties(string key)
        {
            return _secureProperties?.GetAllProperties(key) ?? new List<SecureProperty>();
        }

        public HashSet<string> GetAllChildLabels(string key)
        {
            return _secureProperties?.GetAllChildLabels(key) ?? new HashSet<string>();
        }

        public HashSet<string> GetAllLabels()
        {
            return _secureProperties?.GetAllLabels() ?? new HashSet<string>();
        }

        public void AddProperty(string key, SecureString value, bool encrypt)
        {
            if (encrypt && !_secureMode)
            {
                throw new SecureStorageException(SECURE_MODE_NOT_ON);
            }

            string? addValue = null;

            if (encrypt)
            {
                addValue = Protect(value);
                value.DestroyValue();
            }
            else
            {
                addValue = value.ToString();
            }

            var secureProperty = SecureProperty.CreateNewSecureProperty(key, addValue!, encrypt);
            AddProperty(secureProperty);
        }

        public SecureProperty Secure(SecureProperty secureProperty)
        {
            var secured = Protect(new SecureString(secureProperty.Value));

            var secProp = SecureProperty.CreateNewSecureProperty(
                SecureProperty.CreateKeyWithSeparator(secureProperty.Key),
                secured ?? string.Empty,
                true);

            AddProperty(secProp);
            return secProp;
        }

        public SecureProperty Unsecure(SecureProperty secureProperty)
        {
            var unprotected = Unprotect(secureProperty.Value);

            var secpr = SecureProperty.CreateNewSecureProperty(
                SecureProperty.CreateKeyWithSeparator(secureProperty.Key),
                unprotected?.ToString() ?? string.Empty,
                false);

            AddProperty(secpr);
            return secpr;
        }

        public void AddUnsecuredProperty(string key, string value)
        {
            AddProperty(key, new SecureString(value), false);
        }

        public void AddUnsecuredProperty(LinkedHashSet<string> key, string value)
        {
            AddProperty(SecureProperty.CreateKeyWithSeparator(key), new SecureString(value), false);
        }

        public void AddSecuredProperty(string key, SecureString value)
        {
            AddProperty(key, value, true);
        }

        public void AddSecuredProperty(LinkedHashSet<string> key, SecureString value)
        {
            AddProperty(SecureProperty.CreateKeyWithSeparator(key), value, true);
        }

        public SecureProperty? GetProperty(string key)
        {
            return _secureProperties?.GetProperty(key);
        }

        public string? GetPropertyStringValue(string key)
        {
            try
            {
                var secureString = GetPropertyValue(key);
                return secureString?.ToString();
            }
            catch (Exception)
            {
                return null;
            }
        }

        public SecureString? Unprotect(SecureProperty secureProperty)
        {
            return Unprotect(secureProperty.Value);
        }

        public bool HasProperty(string key)
        {
            var secureProperty = GetProperty(key);
            return secureProperty != null;
        }

        public SecureString? GetPropertyValue(string key)
        {
            var secureProperty = GetProperty(key);
            if (secureProperty == null)
            {
                Log.Info($"GetPropertyValue('{key}'): property not found in map");
                return null;
            }

            Log.Info($"GetPropertyValue('{key}'): found, encrypted={secureProperty.IsEncrypted}, masterPassword set={_masterPassword != null}, secureMode={_secureMode}");

            if (secureProperty.IsEncrypted)
            {
                var secureString = Unprotect(secureProperty.Value);
                Log.Info($"GetPropertyValue('{key}'): decrypt result null={secureString == null}");
                return secureString;
            }

            return new SecureString(secureProperty.Value);
        }

        public Dictionary<string, string> GetAllPropertiesAsMap(string key)
        {
            var properties = GetAllProperties(key);
            var retValue = new Dictionary<string, string>();

            foreach (var property in properties)
            {
                if (property.Value != null)
                {
                    retValue[property.GetValueKey()] = property.Value;
                }
            }

            return retValue;
        }

        private string? Protect(SecureString secureString)
        {
            if (secureString.Value == null) return null;

            if (_masterPassword != null)
            {
                string? encrypted = null;
                try
                {
                    encrypted = new Enc().Encrypt(secureString.ToString()!, _masterPassword.ToString()!);
                }
                catch (Exception)
                {
                    // Log error if needed
                }

                secureString.DestroyValue();
                return encrypted;
            }

            return null;
        }

        private SecureString? Unprotect(string? protectedValue)
        {
            if (protectedValue == null)
            {
                Log.Info("Unprotect: protectedValue is null");
                return null;
            }

            if (_masterPassword != null)
            {
                SecureString? decrypted = null;
                try
                {
                    decrypted = new SecureString(new Enc().Decrypt(protectedValue, _masterPassword.ToString()!));
                    Log.Info("Unprotect: decryption succeeded");
                }
                catch (Exception ex)
                {
                    Log.Info($"Unprotect: decryption FAILED: {ex.GetType().Name}: {ex.Message}");
                }

                return decrypted;
            }

            Log.Info("Unprotect: _masterPassword is null, cannot decrypt");
            return null;
        }

        public void DeleteProperty(SecureProperty secureProperty)
        {
            _secureProperties?.DeleteProperty(secureProperty);
            _secureProperties?.SaveProperties();
        }

        public void DeleteProperty(string key)
        {
            var secureProperty = _secureProperties?.GetProperty(key);
            if (secureProperty != null)
            {
                _secureProperties?.DeleteProperty(secureProperty);
                _secureProperties?.SaveProperties();
            }
        }

        /// <summary>
        /// Initialize secured properties file. Creates if it doesn't exist, opens if it does.
        /// When creating, auto-generates a password and saves it to a txt file in the same directory.
        /// When opening, handles re-encryption if the file was encrypted by another Windows user.
        /// Also processes any properties with {ENC} prefix (encrypts and stores them).
        /// </summary>
        public static SecStorage InitSecuredProperties(string fileName)
        {
            fileName = Path.GetFullPath(fileName);
            if (!fileName.EndsWith(".properties", StringComparison.OrdinalIgnoreCase))
                fileName += ".properties";

            if (!File.Exists(fileName))
            {
                // Create new secured storage with auto-generated password
                var password = new Enc().GeneratePassword();
                var securePassword = new SecureString(password);
                CreateNewSecureStorage(fileName, securePassword, createSecured: true);
                SavePasswordToFile(fileName, password);
                return OpenSecuredStorage(fileName, new SecureString(password));
            }

            // File exists - try to open

            // Try Windows DPAPI first
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                try
                {
                    if (IsWindowsSecured(fileName))
                    {
                        var storage = OpenSecuredStorageWithWindows(fileName);
                        // Process any {ENC} entries
                        SecureEntries();
                        return storage;
                    }
                }
                catch (Exception)
                {
                    // DPAPI failed (encrypted with different user/machine), fall through to password recovery
                }
            }

            // Need master password - try multiple sources
            string? masterPasswordStr = null;
            bool foundInPropertiesFile = false;

            // 1. Try password from companion txt file
            masterPasswordStr = ReadPasswordFromFile(fileName);

            // 2. Try MASTER_PASSWORD from properties file itself
            if (masterPasswordStr == null)
            {
                masterPasswordStr = ReadMasterPasswordFromProperties(fileName);
                if (masterPasswordStr != null)
                    foundInPropertiesFile = true;
            }

            if (masterPasswordStr == null)
            {
                throw new SecureStorageException(
                    "Cannot open properties file. Open the properties file, add MASTER_PASSWORD=XXX as plain text and restart the program");
            }

            var securePass = new SecureString(masterPasswordStr);
            SecStorage secStorage;

            try
            {
                secStorage = OpenSecuredStorage(fileName, securePass);
            }
            catch (SecureStorageException ex) when (
                ex.Message == PASSWORD_NOT_CORRECT ||
                ex.Message == MASTER_KEY_NOT_SET)
            {
                // Password hash missing or corrupted - reconstruct it using the known password
                _storage = new SecStorage();
                _storage._secureProperties = SecureProperties.OpenSecuredProperties(fileName, checkVersion: false);

                var saltedHash = new Enc().GetSaltedHash(new SecureString(masterPasswordStr).Value!);
                _storage.AddUnsecuredProperty(MASTER_PASSWORD_HASH, saltedHash);

                // Ensure version marker exists
                if (!_storage.HasProperty(ENC_VERSION))
                    _storage.AddUnsecuredProperty(ENC_VERSION, "2");

                // Now open normally with the reconstructed hash
                secStorage = OpenSecuredStorage(fileName, new SecureString(masterPasswordStr));
            }

            // Remove MASTER_PASSWORD plain text from properties file if it was there
            if (foundInPropertiesFile)
            {
                secStorage.DeleteProperty(MASTER_PASSWORD_KEY);
            }

            // Process any {ENC} entries
            SecureEntries();

            return secStorage;
        }

        /// <summary>
        /// Get the path to the companion password file for a properties file
        /// </summary>
        public static string GetPasswordFilePath(string propertiesFilePath)
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(propertiesFilePath));
            return Path.Combine(dir ?? ".", PASSWORD_FILE_NAME);
        }

        /// <summary>
        /// Save a password to the companion txt file in the same directory as the properties file
        /// </summary>
        public static void SavePasswordToFile(string propertiesFilePath, string password)
        {
            var passwordFilePath = GetPasswordFilePath(propertiesFilePath);
            File.WriteAllText(passwordFilePath, password);
        }

        /// <summary>
        /// Read password from the companion txt file
        /// </summary>
        private static string? ReadPasswordFromFile(string propertiesFilePath)
        {
            var passwordFilePath = GetPasswordFilePath(propertiesFilePath);
            if (!File.Exists(passwordFilePath)) return null;
            var password = File.ReadAllText(passwordFilePath).Trim();
            return string.IsNullOrEmpty(password) ? null : password;
        }

        /// <summary>
        /// Read MASTER_PASSWORD=xxx plain text entry from a properties file
        /// </summary>
        private static string? ReadMasterPasswordFromProperties(string fileName)
        {
            if (!File.Exists(fileName)) return null;

            foreach (var line in File.ReadAllLines(fileName))
            {
                if (line.StartsWith(MASTER_PASSWORD_KEY + "=", StringComparison.OrdinalIgnoreCase))
                {
                    var value = line.Substring(MASTER_PASSWORD_KEY.Length + 1).Trim();
                    // Only accept plain text (not encrypted values)
                    if (!string.IsNullOrEmpty(value) && !value.StartsWith("{ENC}"))
                        return value;
                }
            }

            return null;
        }

        /// <summary>
        /// Check if Windows DPAPI is supported
        /// </summary>
        private static bool IsWindowsSupported()
        {
            return Environment.OSVersion.Platform == PlatformID.Win32NT;
        }

        /// <summary>
        /// Check if storage is secured with Windows DPAPI
        /// </summary>
        public static bool IsWindowsSecured(string fileName)
        {
            try
            {
                _storage = new SecStorage();
                _storage._secureProperties = SecureProperties.OpenSecuredProperties(fileName, checkVersion: true);
                return _storage.HasProperty(MASTER_PASSWORD_WIN_SECURED);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if storage is secured with current Windows user
        /// </summary>
        public static bool IsSecuredWithCurrentUser(string fileName)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) return false;

            try
            {
                _storage = new SecStorage();
                _storage._secureProperties = SecureProperties.OpenSecuredProperties(fileName, checkVersion: true);
                _storage.CheckWindowsSecurity();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Open storage using Windows DPAPI (no password needed)
        /// </summary>
        public static SecStorage OpenSecuredStorageWithWindows(string fileName)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                throw new SecureStorageException(NOT_WINDOWS_SUPPORTED);
            }

            _storage = new SecStorage();
            _storage._secureProperties = SecureProperties.OpenSecuredProperties(fileName, checkVersion: true);

            var hash = _storage.GetPropertyValue(MASTER_PASSWORD_HASH);
            if (hash == null || hash.Value == null)
            {
                throw new SecureStorageException(MASTER_KEY_NOT_SET);
            }

            _storage.CheckWindowsSecurity();
            _storage._secureMode = true;
            SecureEntries();

            return _storage;
        }

        /// <summary>
        /// Add Windows DPAPI encryption for the master password
        /// </summary>
        private void AddWindowsCheck(SecureString masterPassword)
        {
            if (!IsWindowsSupported())
            {
                throw new SecureStorageException(NOT_WINDOWS_SUPPORTED);
            }

            // Encrypt master password using Windows DPAPI
            byte[] passwordBytes = Encoding.UTF8.GetBytes(masterPassword.ToString()!);
            byte[] encryptedBytes = ProtectedData.Protect(
                passwordBytes,
                null,
                DataProtectionScope.CurrentUser);
            string encoded = Convert.ToBase64String(encryptedBytes);

            // Store encrypted master password
            var secureProperty = SecureProperty.CreateNewSecureProperty(
                MASTER_PASSWORD_WIN_SECURED,
                encoded,
                true);
            AddProperty(secureProperty);

            // Store test string to verify decryption
            _secureMode = true;
            _masterPassword = masterPassword;
            AddSecuredProperty(TEST_STRING, new SecureString(TEST_STRING));

            // Clear password bytes from memory
            Array.Clear(passwordBytes, 0, passwordBytes.Length);
        }

        /// <summary>
        /// Check Windows DPAPI security by decrypting the test value
        /// </summary>
        private void CheckWindowsSecurity()
        {
            Log.Info("CheckWindowsSecurity: starting...");

            if (_secureProperties?.GetProperty(MASTER_PASSWORD_WIN_SECURED) == null)
            {
                Log.Info("CheckWindowsSecurity: MASTER_PASSWORD_WIN_SECURED not found");
                throw new SecureStorageException(NO_MASTER_KEY);
            }

            var encrypted = _secureProperties.GetProperty(TEST_STRING);
            if (encrypted == null)
            {
                Log.Info("CheckWindowsSecurity: TEST_STRING (STORAGE.WINDOWS_SECURED) not found");
                throw new SecureStorageException("Windows check key missing");
            }
            Log.Info($"CheckWindowsSecurity: test string found, encrypted={encrypted.IsEncrypted}, value length={encrypted.Value?.Length}");

            // Retrieve and decrypt master password using DPAPI
            var masterPasswordProp = _secureProperties.GetProperty(MASTER_PASSWORD_WIN_SECURED);
            if (masterPasswordProp?.Value == null)
            {
                Log.Info("CheckWindowsSecurity: masterPasswordProp value is null");
                throw new SecureStorageException(NO_MASTER_KEY);
            }
            Log.Info($"CheckWindowsSecurity: DPAPI blob length={masterPasswordProp.Value.Length}");

            Log.Info("CheckWindowsSecurity: calling ProtectedData.Unprotect...");
            byte[] encryptedBytes = Convert.FromBase64String(masterPasswordProp.Value.Replace("{ENC}", ""));
            byte[] decryptedBytes = ProtectedData.Unprotect(
                encryptedBytes,
                null,
                DataProtectionScope.CurrentUser);
            string decryptedPassword = Encoding.UTF8.GetString(decryptedBytes);
            _masterPassword = new SecureString(decryptedPassword.ToCharArray());
            Log.Info($"CheckWindowsSecurity: DPAPI decrypted OK, password length={decryptedPassword.Length}, first2='{decryptedPassword.Substring(0, Math.Min(2, decryptedPassword.Length))}..', hash={decryptedPassword.GetHashCode()}");

            // Verify by decrypting the test value
            Log.Info("CheckWindowsSecurity: verifying test string decryption...");
            var decrypted = Unprotect(encrypted.Value);
            if (decrypted == null || !decrypted.ToString()!.Equals(TEST_STRING))
            {
                Log.Info($"CheckWindowsSecurity: test string verification FAILED (decrypted null={decrypted == null})");
                throw new SecureStorageException("Windows encrypted with other user");
            }
            Log.Info("CheckWindowsSecurity: test string verification OK");

            // Clear decrypted password from memory
            Array.Clear(decryptedBytes, 0, decryptedBytes.Length);
        }
    }
}
