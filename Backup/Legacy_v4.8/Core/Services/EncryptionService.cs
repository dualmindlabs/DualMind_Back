using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using DualMind_Back.Infrastructure.Configuration;

namespace DualMind_Back.Core.Services
{
    /// <summary>
    /// Simple AES-256 encryption for storing API keys.
    /// Uses APP_SECRET from env as the key, or a default key if missing.
    /// </summary>
    public class EncryptionService
    {
        private readonly byte[] _key;

        public EncryptionService()
        {
            EnvConfig.Load();
            var secret = EnvConfig.AppSecret;
            
            if (string.IsNullOrWhiteSpace(secret))
            {
                // Use a default key if APP_SECRET is missing
                secret = "DefaultDualMindEncryptionKey2024!@#$%^&*()";
            }

            // Ensure key is exactly 32 bytes
            using (var sha = SHA256.Create())
            {
                _key = sha.ComputeHash(Encoding.UTF8.GetBytes(secret));
            }
        }

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;

            using (var item = Aes.Create())
            {
                item.Key = _key;
                item.GenerateIV();
                var iv = item.IV;

                using (var encryptor = item.CreateEncryptor(item.Key, iv))
                using (var ms = new MemoryStream())
                {
                    // Prepend IV to the stream
                    ms.Write(iv, 0, iv.Length);
                    
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    using (var sw = new StreamWriter(cs))
                    {
                        sw.Write(plainText);
                    }
                    
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;

            var fullCipher = Convert.FromBase64String(cipherText);

            using (var item = Aes.Create())
            {
                item.Key = _key;
                
                // Extract IV (first 16 bytes for AES)
                var iv = new byte[16];
                if (fullCipher.Length < 16) throw new Exception("Invalid cipher text");
                Array.Copy(fullCipher, 0, iv, 0, 16);
                item.IV = iv;

                using (var decryptor = item.CreateDecryptor(item.Key, iv))
                using (var ms = new MemoryStream(fullCipher, 16, fullCipher.Length - 16))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var sr = new StreamReader(cs))
                {
                    return sr.ReadToEnd();
                }
            }
        }
    }
}
