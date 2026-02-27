using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SecuredPropertiesCSharp
{
    public class Enc
    {
        private const int Iterations = 500 * 1000;
        private const int DesiredKeyLen = 256;
        private const int SaltLen = 64;
        private static readonly Encoding Encoding = Encoding.UTF8;
        private const string EncryptionAlg = "AES";

        /// <summary>
        /// Generates a hash from password and salt using PBKDF2
        /// </summary>
        public string Hash(char[] password, byte[] salt)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(new string(password), salt, Iterations, HashAlgorithmName.SHA256))
            {
                byte[] hash = pbkdf2.GetBytes(DesiredKeyLen / 8);
                return Convert.ToBase64String(hash);
            }
        }

        /// <summary>
        /// Generates a salted hash of the password
        /// </summary>
        public string GetSaltedHash(char[] password)
        {
            byte[] salt = new byte[SaltLen];
            new Random().NextBytes(salt);
            return Convert.ToBase64String(salt) + "$" + Hash(password, salt);
        }

        /// <summary>
        /// Checks whether given plaintext password corresponds to a stored salted hash
        /// </summary>
        public bool Check(string password, string stored)
        {
            string[] saltAndPass = stored.Split('$');
            
            if (saltAndPass.Length != 2)
                throw new ArgumentException("The stored password must have the form 'salt$hash'");

            byte[] salt = Convert.FromBase64String(saltAndPass[0]);
            string hashOfInput = Hash(password.ToCharArray(), salt);
            
            return hashOfInput == saltAndPass[1];
        }

        /// <summary>
        /// Derives a key from password and salt
        /// </summary>
        private byte[] GetKeyFromPassword(string password, byte[] salt)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
            {
                return pbkdf2.GetBytes(DesiredKeyLen / 8);
            }
        }

        /// <summary>
        /// Generates a random IV (Initialization Vector)
        /// </summary>
        private byte[] GenerateIv()
        {
            byte[] iv = new byte[16];
            new Random().NextBytes(iv);
            return iv;
        }

        /// <summary>
        /// Encrypts the given clear text using AES encryption
        /// </summary>
        public string Encrypt(string clearText, string key)
        {
            // Generate a random salt
            byte[] salt = new byte[SaltLen];
            new Random().NextBytes(salt);
            
            // Derive a key using the password and the random salt
            byte[] keyBytes = GetKeyFromPassword(key, salt);
            
            // Generate a random IV for each encryption
            byte[] iv = GenerateIv();
            
            // Encrypt the clear text
            byte[] cipherText;
            using (Aes aes = Aes.Create())
            {
                aes.Key = keyBytes;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                
                using var encryptor = aes.CreateEncryptor();
                cipherText = encryptor.TransformFinalBlock(
                    Encoding.GetBytes(clearText), 
                    0, 
                    Encoding.GetByteCount(clearText));
            }
            
            // Combine salt, IV, and cipher text into a single byte array
            byte[] combined = new byte[salt.Length + iv.Length + cipherText.Length];
            Buffer.BlockCopy(salt, 0, combined, 0, salt.Length);
            Buffer.BlockCopy(iv, 0, combined, salt.Length, iv.Length);
            Buffer.BlockCopy(cipherText, 0, combined, salt.Length + iv.Length, cipherText.Length);
            
            // Return the encoded combined byte array as a base64 string
            return Convert.ToBase64String(combined);
        }

        /// <summary>
        /// Generates a random password with specified character types
        /// </summary>
        public string GeneratePassword(int lowerCase = 6, int upperCase = 8, int numbers = 10, int symbols = 6)
        {
            const string lowercaseChars = "abcdefghijklmnopqrstuvwxyz";
            const string uppercaseChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string numberChars = "0123456789";
            const string symbolChars = "?!@#%{}[]+-*@_=<>";
            
            var password = new List<char>();
            
            var random = new Random();
            
            for (int i = 0; i < upperCase; i++)
                password.Add(uppercaseChars[random.Next(uppercaseChars.Length)]);
            
            for (int i = 0; i < lowerCase; i++)
                password.Add(lowercaseChars[random.Next(lowercaseChars.Length)]);
            
            for (int i = 0; i < numbers; i++)
                password.Add(numberChars[random.Next(numberChars.Length)]);
            
            for (int i = 0; i < symbols; i++)
                password.Add(symbolChars[random.Next(symbolChars.Length)]);
            
            // Shuffle the password characters
            return new string(password.OrderBy(_ => random.Next()).ToArray());
        }

        /// <summary>
        /// Decrypts the given encrypted text using AES decryption
        /// </summary>
        public string Decrypt(string encrypted, string key)
        {
            byte[] decoded = Convert.FromBase64String(encrypted);
            
            // Extract salt, IV, and encrypted text
            byte[] salt = new byte[SaltLen];
            byte[] iv = new byte[16];
            byte[] encryptedText = new byte[decoded.Length - SaltLen - 16];
            
            Buffer.BlockCopy(decoded, 0, salt, 0, SaltLen);
            Buffer.BlockCopy(decoded, SaltLen, iv, 0, 16);
            Buffer.BlockCopy(decoded, SaltLen + 16, encryptedText, 0, encryptedText.Length);
            
            // Derive the key
            byte[] keyBytes = GetKeyFromPassword(key, salt);
            
            // Decrypt the text
            byte[] plainText;
            using (Aes aes = Aes.Create())
            {
                aes.Key = keyBytes;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                
                using var decryptor = aes.CreateDecryptor();
                plainText = decryptor.TransformFinalBlock(encryptedText, 0, encryptedText.Length);
            }
            
            return Encoding.GetString(plainText);
        }
    }
}
