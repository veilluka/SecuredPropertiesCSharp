using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SecuredPropertiesCSharp
{
    public class SecureStorageException : Exception
    {
        public const string FILE_NOT_EXISTS = "File does not exist";
        public const string FILE_EXISTS_ALREADY = "File already exists";
        public const string OLD_VERSION = "Old version detected";
        public const string PASSWORD_TOO_SHORT = "Password must be at least 12 characters";
        public const string NOT_WINDOWS_SUPPORTED = "Windows DPAPI is not supported";
        public const string MASTER_KEY_NOT_SET = "Master key is not set";
        public const string PASSWORD_NOT_CORRECT = "Password is not correct";
        public const string NO_MASTER_KEY = "No master key found";
        public const string SECURE_MODE_NOT_ON = "Secure mode is not enabled";

        public SecureStorageException(string message) : base(message) { }
    }

    public class SecureProperties
    {
        // Storage constants
        public const string TEST_STRING = "STORAGE.WINDOWS_SECURED";
        public const string MASTER_PASSWORD_WIN_SECURED = "STORAGE.MASTER_PASSWORD_WINDOWS_SECURED";
        public const string MASTER_PASSWORD_HASH = "STORAGE.MASTER_PASSWORD_HASH";
        public const string ENC_VERSION = "STORAGE.ENC_VERSION";

        internal Dictionary<LinkedHashSet<string>, List<SecureProperty>> _map;
        private string? _filePath;

        public SecureProperties()
        {
            _map = new Dictionary<LinkedHashSet<string>, List<SecureProperty>>(new LinkedHashSetComparer());
        }

        public string? GetStringProperty(string key)
        {
            return GetStringProperty(SecureProperty.ParseKey(key));
        }

        public string? GetStringProperty(string[] key)
        {
            var secureProperty = GetProperty(key);
            return secureProperty?.Value;
        }

        public bool GetBooleanValue(string key)
        {
            return GetBooleanValue(SecureProperty.ParseKey(key));
        }

        public bool GetBooleanValue(string[] key)
        {
            var val = GetStringProperty(key);
            if (val == null) return false;
            return val.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public void AddStringProperty(string[] key, string value, bool encrypted)
        {
            AddProperty(SecureProperty.CreateNewSecureProperty(key, value, encrypted));
        }

        public void AddBooleanProperty(string[] key, bool value)
        {
            var booleanValue = value ? "true" : "false";
            var secureProperty = SecureProperty.CreateNewSecureProperty(key, booleanValue, false);
            AddProperty(secureProperty);
        }

        public SecureProperty? GetProperty(string key)
        {
            return GetProperty(SecureProperty.ParseKey(key));
        }

        public List<SecureProperty> GetAllProperties(string key)
        {
            var retValue = new List<SecureProperty>();
            var compareWith = SecureProperty.CreateKey(key);

            foreach (var k in _map.Keys)
            {
                if (SecureProperty.CreateKeyWithSeparator(k).Contains("STORAGE."))
                    continue;

                var keyProperties = _map[k];
                foreach (var secureProperty in keyProperties)
                {
                    if (secureProperty.IsSubKeyOf(compareWith))
                        retValue.Add(secureProperty);
                }
            }

            return retValue;
        }

        public HashSet<string> GetAllChildLabels(string key)
        {
            var retValue = new HashSet<string>();
            var compareWith = SecureProperty.CreateKey(key);

            foreach (var k in _map.Keys)
            {
                if (SecureProperty.CreateKeyWithSeparator(k).Contains("STORAGE."))
                    continue;

                var keyProperties = _map[k];
                foreach (var secureProperty in keyProperties)
                {
                    if (secureProperty.IsChildOf(compareWith))
                    {
                        var propertyKey = new LinkedHashSet<string>(secureProperty.Key);
                        var stringBuilder = new StringBuilder();
                        stringBuilder.Append(SecureProperty.CreateKeyWithSeparator(compareWith));

                        if (stringBuilder.Length > 0)
                            stringBuilder.Append(".");

                        propertyKey.RemoveAll(compareWith);
                        var it = propertyKey.GetEnumerator();
                        if (it.MoveNext())
                            stringBuilder.Append(it.Current);

                        retValue.Add(stringBuilder.ToString());
                    }
                }
            }

            return retValue;
        }

        public HashSet<string> GetAllLabels()
        {
            var retValue = new HashSet<string>();

            foreach (var k in _map.Keys)
            {
                if (SecureProperty.CreateKeyWithSeparator(k).Contains("STORAGE."))
                    continue;

                var stringBuilder = new StringBuilder();
                var array = k.ToArray();

                for (int i = 0; i < array.Length - 1; i++)
                {
                    stringBuilder.Append(array[i]);
                    stringBuilder.Append(".");
                }

                if (stringBuilder.Length > 0)
                {
                    stringBuilder.Remove(stringBuilder.Length - 1, 1);
                }

                if (stringBuilder.Length > 0)
                    retValue.Add(stringBuilder.ToString());
                else
                    retValue.Add("");
            }

            return retValue;
        }

        public List<string> GetAllKeys()
        {
            var retValue = new List<string>();

            foreach (var k in _map.Keys)
            {
                if (SecureProperty.CreateKeyWithSeparator(k).Contains("STORAGE."))
                    continue;

                retValue.Add(SecureProperty.CreateKeyWithSeparator(k));
            }

            return retValue;
        }

        public SecureProperty? GetProperty(string[] key)
        {
            foreach (var s in _map.Keys)
            {
                var properties = _map[s];
                foreach (var secureProperty in properties)
                {
                    if (secureProperty.IsKeyEqual(key))
                        return secureProperty;
                }
            }

            return null;
        }

        public void AddProperty(SecureProperty secureProperty)
        {
            if (!HasUnorderedProperty(secureProperty))
            {
                if (!_map.ContainsKey(secureProperty.Key))
                {
                    _map[secureProperty.Key] = new List<SecureProperty>();
                }
                _map[secureProperty.Key].Add(secureProperty);
                return;
            }

            var foundProperties = _map[secureProperty.Key];
            foundProperties.RemoveAll(s => s.IsKeyEqual(secureProperty.Key));
            foundProperties.Add(secureProperty);
        }

        public void DeleteProperty(SecureProperty secureProperty)
        {
            if (!HasUnorderedProperty(secureProperty))
                return;

            var foundProperties = _map[secureProperty.Key];
            foundProperties.RemoveAll(s => s.IsKeyEqual(secureProperty.Key));
        }

        private bool HasUnorderedProperty(SecureProperty secureProperty)
        {
            if (_map.Count == 0) return false;
            if (!_map.ContainsKey(secureProperty.Key)) return false;
            if (_map[secureProperty.Key].Count == 0) return false;
            return true;
        }

        public void SaveProperties()
        {
            if (_filePath == null)
                throw new InvalidOperationException("File path is not set");

            var fileName = _filePath.Replace(".json", ".properties");
            if (!fileName.EndsWith("properties"))
                fileName += ".properties";

            using var writer = new StreamWriter(fileName, false, Encoding.UTF8);
            
            var data = new List<SecureProperty>();
            var header = new List<SecureProperty>();
            
            data = AddData(data);
            data.Sort();
            header = AddHeader(header);

            writer.WriteLine("-------------------------------@@HEADER_START@@------------------------------------------------------------- ");
            foreach (var secureProperty in header)
                CreateFileEntry(secureProperty, writer);

            writer.WriteLine("-------------------------------@@HEADER_END@@-------------------------------------------------------------");
            foreach (var secureProperty in data)
                CreateFileEntry(secureProperty, writer);
        }

        private void CreateFileEntry(SecureProperty secureProperty, StreamWriter writer)
        {
            var key = SecureProperty.CreateKeyWithSeparator(secureProperty.Key);
            writer.Write(key);
            writer.Write("=");

            if (secureProperty.IsEncrypted)
            {
                writer.Write("{ENC}");
                if (secureProperty.Value != null)
                    writer.Write(secureProperty.Value);
                writer.Write("{ENC}");
            }
            else
            {
                if (secureProperty.Value != null)
                    writer.Write(secureProperty.Value);
            }

            writer.WriteLine();
        }

        private List<SecureProperty> AddData(List<SecureProperty> export)
        {
            var hash = SecureProperty.CreateKey(MASTER_PASSWORD_HASH);
            var win = SecureProperty.CreateKey(MASTER_PASSWORD_WIN_SECURED);
            var test = SecureProperty.CreateKey(TEST_STRING);
            var vers = SecureProperty.CreateKey(ENC_VERSION);
            var comparer = new LinkedHashSetComparer();

            foreach (var key in _map.Keys)
            {
                if (comparer.Equals(key, hash)) continue;
                if (comparer.Equals(key, win)) continue;
                if (comparer.Equals(key, test)) continue;
                if (comparer.Equals(key, vers)) continue;

                var secureProperty = _map[key];
                export.AddRange(secureProperty);
            }

            return export;
        }

        private List<SecureProperty> AddHeader(List<SecureProperty> export)
        {
            var hash = SecureProperty.CreateKey(MASTER_PASSWORD_HASH);
            var win = SecureProperty.CreateKey(MASTER_PASSWORD_WIN_SECURED);
            var test = SecureProperty.CreateKey(TEST_STRING);
            var vers = SecureProperty.CreateKey(ENC_VERSION);

            AddHeaderProperties(export, hash);
            AddHeaderProperties(export, win);
            AddHeaderProperties(export, test);
            AddHeaderProperties(export, vers);

            return export;
        }

        private void AddHeaderProperties(List<SecureProperty> export, LinkedHashSet<string> key)
        {
            if (_map.ContainsKey(key))
            {
                export.AddRange(_map[key]);
            }
        }

        public static SecureProperties OpenSecuredProperties(string fileName, bool checkVersion = false)
        {
            var secureProperties = new SecureProperties();
            secureProperties._filePath = fileName;

            if (!File.Exists(fileName))
                throw new SecureStorageException(SecureStorageException.FILE_NOT_EXISTS);

            using var reader = new StreamReader(fileName, Encoding.UTF8);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Contains("@@HEADER_START@@") || line.Contains("@@HEADER_END@@"))
                    continue;

                var secureProperty = secureProperties.ReadFileEntry(line);
                if (secureProperty != null)
                    secureProperties.AddProperty(secureProperty);
            }

            if (checkVersion)
            {
                var versionProperty = secureProperties.GetProperty(ENC_VERSION);
                if (versionProperty != null && versionProperty.Value?.Equals("2", StringComparison.OrdinalIgnoreCase) == true)
                {
                    // Version check passed
                }
                else
                {
                    throw new SecureStorageException(SecureStorageException.OLD_VERSION);
                }
            }

            return secureProperties;
        }

        private SecureProperty? ReadFileEntry(string line)
        {
            if (line == null) return null;

            var posEqual = line.IndexOf('=');
            if (posEqual == -1) return null;

            var keyString = line.Substring(0, posEqual);
            var secureProperty = new SecureProperty();
            secureProperty.SetKey(SecureProperty.CreateKey(keyString));

            var valueString = line.Substring(posEqual + 1);

            if (string.IsNullOrEmpty(valueString))
            {
                secureProperty.IsEncrypted = false;
            }
            else
            {
                if (valueString.StartsWith("{ENC}") && valueString.EndsWith("{ENC}"))
                {
                    var val = valueString.Replace("{ENC}", "");
                    secureProperty.Value = val;
                    secureProperty.IsEncrypted = true;
                }
                else
                {
                    secureProperty.Value = valueString;
                    secureProperty.IsEncrypted = false;
                }
            }

            return secureProperty;
        }

        public static SecureProperties CreateSecuredProperties(string fileName)
        {
            var secureProperties = new SecureProperties();
            secureProperties._filePath = fileName;

            if (File.Exists(fileName))
            {
                using var reader = new StreamReader(fileName, Encoding.UTF8);
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Contains("@@HEADER_START@@") || line.Contains("@@HEADER_END@@"))
                    {
                        throw new SecureStorageException(SecureStorageException.FILE_EXISTS_ALREADY);
                    }

                    var secureProperty = secureProperties.ReadFileEntry(line);
                    if (secureProperty != null)
                        secureProperties.AddProperty(secureProperty);
                }
            }

            secureProperties.SaveProperties();
            return secureProperties;
        }

        public override string ToString()
        {
            return $"SecureProperties{{ _map={_map}, _filePath={_filePath} }}";
        }
    }

    /// <summary>
    /// Comparer for LinkedHashSet to use in Dictionary
    /// </summary>
    public class LinkedHashSetComparer : IEqualityComparer<LinkedHashSet<string>>
    {
        public bool Equals(LinkedHashSet<string>? x, LinkedHashSet<string>? y)
        {
            if (x == null && y == null) return true;
            if (x == null || y == null) return false;
            if (x.Count != y.Count) return false;

            var xArray = x.ToArray();
            var yArray = y.ToArray();

            for (int i = 0; i < xArray.Length; i++)
            {
                if (!xArray[i].Equals(yArray[i], StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        public int GetHashCode(LinkedHashSet<string> obj)
        {
            if (obj == null) return 0;

            unchecked
            {
                int hash = 17;
                foreach (var item in obj)
                {
                    hash = hash * 31 + (item?.ToLowerInvariant().GetHashCode() ?? 0);
                }
                return hash;
            }
        }
    }
}
