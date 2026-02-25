using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SecuredPropertiesCSharp
{
    public class SecStorage
    {
        // Storage constants
        public const string TEST_STRING = "STORAGE@@WINDOWS_SECURED";
        public const string MASTER_PASSWORD_WIN_SECURED = "STORAGE@@MASTER_PASSWORD_WINDOWS_SECURED";
        public const string MASTER_PASSWORD_HASH = "STORAGE@@MASTER_PASSWORD_HASH";
        public const string ENC_VERSION = "STORAGE@@ENC_VERSION";

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
        private bool _winDPAPISupported = false;

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
            if (IsWindowsSupported())
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
            if (masterPassword == null)
            {
                throw new SecureStorageException(MASTER_KEY_NOT_SET);
            }

            _storage = new SecStorage();
            _storage._secureProperties = SecureProperties.OpenSecuredProperties(fileName, checkVersion: true);

            if (!_storage.CheckMasterKey(masterPassword))
                throw new SecureStorageException(PASSWORD_NOT_CORRECT);

            // Try to use Windows DPAPI if available
            if (IsWindowsSupported())
            {
                try
                {
                    _storage.AddWindowsCheck(masterPassword);
                    _storage._secureMode = true;
                    _storage._winDPAPISupported = true;
                }
                catch (Exception)
                {
                    // Fall back to master password mode
                    _storage._secureMode = true;
                    _storage._masterPassword = masterPassword;
                }
            }
            else
            {
                _storage._secureMode = true;
                _storage._masterPassword = masterPassword;
            }

            return _storage;
        }

        /// <summary>
        /// Open storage without security (read-only mode)
        /// </summary>
        public static SecStorage OpenSecuredStorage(string fileName, bool openSecured)
        {
            _storage = new SecStorage();
            _storage._secureProperties = SecureProperties.OpenSecuredProperties(fileName, checkVersion: true);

            if (!openSecured) return _storage;

            var hash = _storage.GetPropertyValue("STORAGE@@MASTER_PASSWORD_HASH");
            if (hash == null || hash.Value == null)
            {
                throw new SecureStorageException(MASTER_KEY_NOT_SET);
            }

            return _storage;
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
                secured,
                true);

            AddProperty(secProp);
            return secProp;
        }

        public SecureProperty Unsecure(SecureProperty secureProperty)
        {
            var unprotected = Unprotect(secureProperty.Value);

            var secpr = SecureProperty.CreateNewSecureProperty(
                SecureProperty.CreateKeyWithSeparator(secureProperty.Key),
                unprotected?.ToString(),
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
            if (secureProperty == null) return null;

            if (secureProperty.IsEncrypted)
            {
                var secureString = Unprotect(secureProperty.Value);
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
            if (protectedValue == null) return null;

            if (_masterPassword != null)
            {
                SecureString? decrypted = null;
                try
                {
                    decrypted = new SecureString(new Enc().Decrypt(protectedValue, _masterPassword.ToString()!));
                }
                catch (Exception)
                {
                    // Log error if needed
                }

                return decrypted;
            }

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
        /// Check if Windows DPAPI is supported
        /// </summary>
        private static bool IsWindowsSupported()
        {
            return OperatingSystem.IsWindows();
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
            if (!IsWindowsSupported()) return false;

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
            if (!IsWindowsSupported())
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
            _storage._winDPAPISupported = true;
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
            if (_secureProperties?.GetProperty(MASTER_PASSWORD_WIN_SECURED) == null)
            {
                throw new SecureStorageException(NO_MASTER_KEY);
            }

            var encrypted = _secureProperties.GetProperty(TEST_STRING);
            if (encrypted == null)
            {
                throw new SecureStorageException("Windows check key missing");
            }

            // Retrieve and decrypt master password using DPAPI
            var masterPasswordProp = _secureProperties.GetProperty(MASTER_PASSWORD_WIN_SECURED);
            if (masterPasswordProp?.Value == null)
            {
                throw new SecureStorageException(NO_MASTER_KEY);
            }

            byte[] encryptedBytes = Convert.FromBase64String(masterPasswordProp.Value.Replace("{ENC}", ""));
            byte[] decryptedBytes = ProtectedData.Unprotect(
                encryptedBytes,
                null,
                DataProtectionScope.CurrentUser);
            string decryptedPassword = Encoding.UTF8.GetString(decryptedBytes);
            _masterPassword = new SecureString(decryptedPassword.ToCharArray());

            // Verify by decrypting the test value
            var decrypted = Unprotect(encrypted.Value);
            if (decrypted == null || !decrypted.ToString()!.Equals(TEST_STRING))
            {
                throw new SecureStorageException("Windows encrypted with other user");
            }

            // Clear decrypted password from memory
            Array.Clear(decryptedBytes, 0, decryptedBytes.Length);
        }
    }
}
