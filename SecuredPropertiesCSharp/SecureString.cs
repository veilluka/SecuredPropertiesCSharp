using System;
using System.Linq;
using System.Text;

namespace SecuredPropertiesCSharp
{
    public class SecureString
    {
        private char[]? _value;

        public char[]? Value
        {
            get => _value;
        }

        public SecureString(string? input)
        {
            if (string.IsNullOrEmpty(input))
            {
                _value = null;
                return;
            }

            _value = input.ToCharArray();
        }

        public SecureString(char[]? input)
        {
            _value = input;
        }

        public void DestroyValue()
        {
            if (_value == null) return;

            for (int i = 0; i < _value.Length; i++)
                _value[i] = '*';

            _value = null;
        }

        public byte[] GetBytes()
        {
            if (_value == null) return Array.Empty<byte>();
            return Encoding.UTF8.GetBytes(_value);
        }

        public override string? ToString()
        {
            if (_value == null) return null;
            return new string(_value);
        }

        public string CleanInvalidCharacters(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            var output = new StringBuilder();

            foreach (char current in input)
            {
                if ((current == 0x9)
                    || (current == 0xA)
                    || (current == 0xD)
                    || ((current >= 0x20) && (current <= 0xD7FF))
                    || ((current >= 0xE000) && (current <= 0xFFFD))
                    || ((current >= 0x10000) && (current <= 0x10FFFF)))
                {
                    output.Append(current);
                }
            }

            return output.ToString().Replace("\\s", " ");
        }

        public char[] CleanInvalidCharacters(char[]? input)
        {
            if (input == null || input.Length == 0)
                return input ?? Array.Empty<char>();

            var output = new StringBuilder();

            foreach (char current in input)
            {
                if ((current == 0x9)
                    || (current == 0xA)
                    || (current == 0xD)
                    || ((current >= 0x20) && (current <= 0xD7FF))
                    || ((current >= 0xE000) && (current <= 0xFFFD))
                    || ((current >= 0x10000) && (current <= 0x10FFFF)))
                {
                    output.Append(current);
                }
            }

            return output.ToString().ToCharArray();
        }

        public SecureString Copy()
        {
            return new SecureString(_value);
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj)) return true;

            if (obj is string str)
            {
                return _value != null && _value.SequenceEqual(str.ToCharArray());
            }

            if (obj == null || GetType() != obj.GetType()) return false;

            var that = (SecureString)obj;
            
            if (_value == null && that._value == null) return true;
            if (_value == null || that._value == null) return false;
            
            return _value.SequenceEqual(that._value);
        }

        public override int GetHashCode()
        {
            if (_value == null) return 0;
            
            unchecked
            {
                int hash = 17;
                foreach (char c in _value)
                {
                    hash = hash * 31 + c;
                }
                return hash;
            }
        }
    }
}
