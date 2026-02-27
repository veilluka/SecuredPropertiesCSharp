using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SecuredPropertiesCSharp
{
    public class SecureProperty : IComparable<SecureProperty>
    {
        private LinkedHashSet<string> _key = new LinkedHashSet<string>();
        private string? _value;
        private bool _encrypted;

        public LinkedHashSet<string> Key
        {
            get => _key;
            set => _key = value;
        }

        public string? Value
        {
            get => _value;
            set => _value = value;
        }

        public bool IsEncrypted
        {
            get => _encrypted;
            set => _encrypted = value;
        }

        public static LinkedHashSet<string> CreateKey(string[] key)
        {
            var linkedHashSet = new LinkedHashSet<string>();
            foreach (var item in key)
            {
                linkedHashSet.Add(item);
            }
            return linkedHashSet;
        }

        public static LinkedHashSet<string> CreateKey(string key)
        {
            return CreateKey(ParseKey(key));
        }

        public static string[] ParseKey(string key)
        {
            if (key == null) return Array.Empty<string>();
            return key.Split(new[] { "." }, StringSplitOptions.None);
        }

        public static string CreateKeyWithSeparator(LinkedHashSet<string> key)
        {
            if (key == null || key.Count == 0) return string.Empty;
            
            var stringBuilder = new StringBuilder();
            foreach (var item in key)
            {
                stringBuilder.Append(item);
                stringBuilder.Append(".");
            }
            
            if (stringBuilder.Length > 0)
            {
                stringBuilder.Remove(stringBuilder.Length - 1, 1);
            }
            
            return stringBuilder.ToString();
        }

        public static SecureProperty CreateNewSecureProperty(string key, string value, bool encrypted)
        {
            return CreateNewSecureProperty(ParseKey(key), value, encrypted);
        }

        public static SecureProperty CreateNewSecureProperty(string[] key, string value, bool encrypted)
        {
            var secureProperty = new SecureProperty();
            
            if (key != null && key.Length > 0)
            {
                secureProperty.SetKey(key);
            }
            
            secureProperty.Value = value;
            secureProperty.IsEncrypted = encrypted;
            
            return secureProperty;
        }

        public static string GetLabel(LinkedHashSet<string> labelWithKey)
        {
            var lblWKey = labelWithKey.ToArray();
            var stringBuilder = new StringBuilder();
            
            for (int i = 0; i < labelWithKey.Count - 1; i++)
            {
                stringBuilder.Append(lblWKey[i]);
                stringBuilder.Append(".");
            }
            
            if (stringBuilder.Length == 0) return string.Empty;
            
            stringBuilder.Remove(stringBuilder.Length - 1, 1);
            return stringBuilder.ToString();
        }

        public SecureProperty AddKey(string key)
        {
            _key.Add(key);
            return this;
        }

        public void SetKey(string[] key)
        {
            foreach (var item in key)
            {
                _key.Add(item);
            }
        }

        public void SetKey(LinkedHashSet<string> key)
        {
            _key = key;
        }

        public string GetValueKey()
        {
            var array = _key.ToArray();
            return array[array.Length - 1];
        }

        public bool IsKeyEqual(LinkedHashSet<string> otherKey)
        {
            if (_key == null && otherKey == null) return true;
            if (_key == null) return false;
            if (otherKey == null) return false;
            
            if (_key.Count != otherKey.Count) return false;
            
            var aArray = _key.ToArray();
            var bArray = otherKey.ToArray();
            
            for (int i = 0; i < aArray.Length; i++)
            {
                if (!aArray[i].Equals(bArray[i], StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            
            return true;
        }

        public bool IsKeyEqual(string[] otherKey)
        {
            var key = new LinkedHashSet<string>();
            foreach (var item in otherKey)
            {
                key.Add(item);
            }
            return IsKeyEqual(key);
        }

        public bool IsSubKeyOf(LinkedHashSet<string> otherKey)
        {
            if (_key.Count == 0) return false;
            if (otherKey.Count == 0) return true;
            if (_key.Count == 1 && otherKey.Count == 1 && otherKey.Contains("")) return true;
            if (otherKey.Count >= _key.Count) return false;
            
            var keyArray = _key.ToArray();
            var otherKeyArray = otherKey.ToArray();
            
            for (int i = 0; i < otherKeyArray.Length; i++)
            {
                if (!keyArray[i].Equals(otherKeyArray[i], StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            
            return true;
        }

        public bool IsChildOf(LinkedHashSet<string> otherKey)
        {
            if (_key.Count == 0) return false;
            if (otherKey.Count == 0) return true;
            if (otherKey.Count + 2 != _key.Count) return false;
            
            var keyArray = _key.ToArray();
            var otherKeyArray = otherKey.ToArray();
            
            for (int i = 0; i < otherKeyArray.Length; i++)
            {
                if (!keyArray[i].Equals(otherKeyArray[i], StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            
            return true;
        }

        public SecureProperty Copy()
        {
            var secureProperty = new SecureProperty
            {
                Value = this.Value,
                Key = new LinkedHashSet<string>(this.Key),
                IsEncrypted = this.IsEncrypted
            };
            
            return secureProperty;
        }

        public override string ToString()
        {
            return $"SecureProperty{{ _key='{_key}', _value='{_value}', _encrypted={_encrypted} }}";
        }

        public int CompareTo(SecureProperty? o)
        {
            if (o == null) return 1;
            return CreateKeyWithSeparator(Key).CompareTo(CreateKeyWithSeparator(o.Key));
        }
    }

    /// <summary>
    /// LinkedHashSet implementation that maintains insertion order
    /// </summary>
    public class LinkedHashSet<T> : ICollection<T> where T : notnull
    {
        private readonly Dictionary<T, LinkedListNode<T>> _dict;
        private readonly LinkedList<T> _list;

        public LinkedHashSet()
        {
            _dict = new Dictionary<T, LinkedListNode<T>>();
            _list = new LinkedList<T>();
        }

        public LinkedHashSet(IEnumerable<T> collection)
        {
            _dict = new Dictionary<T, LinkedListNode<T>>();
            _list = new LinkedList<T>();
            foreach (var item in collection)
            {
                Add(item);
            }
        }

        public int Count => _dict.Count;
        public bool IsReadOnly => false;

        public bool Add(T item)
        {
            if (_dict.ContainsKey(item)) return false;
            
            var node = _list.AddLast(item);
            _dict[item] = node;
            return true;
        }

        void ICollection<T>.Add(T item)
        {
            Add(item);
        }

        public void Clear()
        {
            _dict.Clear();
            _list.Clear();
        }

        public bool Contains(T item)
        {
            return _dict.ContainsKey(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _list.CopyTo(array, arrayIndex);
        }

        public bool Remove(T item)
        {
            if (!_dict.TryGetValue(item, out var node)) return false;
            
            _dict.Remove(item);
            _list.Remove(node);
            return true;
        }

        public void RemoveAll(LinkedHashSet<T> other)
        {
            foreach (var item in other)
            {
                Remove(item);
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public T[] ToArray()
        {
            var array = new T[Count];
            _list.CopyTo(array, 0);
            return array;
        }

        public override string ToString()
        {
            return $"[{string.Join(", ", _list)}]";
        }

        public object Clone()
        {
            return new LinkedHashSet<T>(_list);
        }
    }
}
