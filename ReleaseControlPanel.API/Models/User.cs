using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;

namespace ReleaseControlPanel.API.Models
{
    public class User
    {
        public ObjectId Id { get; set; }

        public DateTime CreatedOn { get; set; } = DateTime.Now;
        public DateTime UpdatedOn { get; set; } = DateTime.Now;


        public bool Admin { get; set; } = false;
        public string CiBuildApiToken { get; set; }
        public string CiBuildUserName { get; set; }
        public string CiQaApiToken { get; set; }
        public string CiQaUserName { get; set; }
        public string CiStagingApiToken { get; set; }
        public string CiStagingUserName { get; set; }
        public string FullName { get; set; }
        public bool IsEncrypted { get; set; }
        public string JiraPassword { get; set; }
        public string JiraUserName { get; set; }
        public string Password { get; set; }
        public string PasswordSalt { get; set; }
        public string UserName { get; set; }
        public string UserSecret { get; set; }

        public void ClearSensitiveData()
        {
            Password = null;
            PasswordSalt = null;
            UserSecret = null;
        }

        public User Clone()
        {
            return MemberwiseClone() as User;
        }

        public void DecryptData(string userSecret)
        {
            if (!IsEncrypted)
                return;

            CiBuildApiToken = DecryptString(CiBuildApiToken, userSecret);
            CiBuildUserName = DecryptString(CiBuildUserName, userSecret);
            CiQaApiToken = DecryptString(CiQaApiToken, userSecret);
            CiQaUserName = DecryptString(CiQaUserName, userSecret);
            CiStagingApiToken = DecryptString(CiStagingApiToken, userSecret);
            CiStagingUserName = DecryptString(CiStagingUserName, userSecret);
            IsEncrypted = false;
            JiraPassword = DecryptString(JiraPassword, userSecret);
            JiraUserName = DecryptString(JiraUserName, userSecret);
        }

        private static string DecryptString(string value, string decryptionKey)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            value = value.Replace(" ", "+");
            var bytesBuff = Convert.FromBase64String(value);
            using (var aes = Aes.Create())
            {
                var crypto = new Rfc2898DeriveBytes(decryptionKey,
                    new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });
                aes.Key = crypto.GetBytes(32);
                aes.IV = crypto.GetBytes(16);
                using (var mStream = new MemoryStream())
                {
                    using (var cStream = new CryptoStream(mStream, aes.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cStream.Write(bytesBuff, 0, bytesBuff.Length);
                    }
                    return Encoding.Unicode.GetString(mStream.ToArray());
                }
            }
        }

        public void DecryptUserSecret(string password)
        {
            UserSecret = DecryptString(UserSecret, password);
        }

        public void EncryptData(string userSecret)
        {
            if (IsEncrypted)
                return;

            CiBuildApiToken = EncryptString(CiBuildApiToken, userSecret);
            CiBuildUserName = EncryptString(CiBuildUserName, userSecret);
            CiQaApiToken = EncryptString(CiQaApiToken, userSecret);
            CiQaUserName = EncryptString(CiQaUserName, userSecret);
            CiStagingApiToken = EncryptString(CiStagingApiToken, userSecret);
            CiStagingUserName = EncryptString(CiStagingUserName, userSecret);
            IsEncrypted = true;
            JiraPassword = EncryptString(JiraPassword, userSecret);
            JiraUserName = EncryptString(JiraUserName, userSecret);
        }

        private static string EncryptString(string value, string encryptionKey)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            var bytesBuff = Encoding.Unicode.GetBytes(value);
            using (var aes = Aes.Create())
            {
                var crypto = new Rfc2898DeriveBytes(encryptionKey, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });
                aes.Key = crypto.GetBytes(32);
                aes.IV = crypto.GetBytes(16);
                using (var mStream = new MemoryStream())
                {
                    using (var cStream = new CryptoStream(mStream, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cStream.Write(bytesBuff, 0, bytesBuff.Length);
                    }

                    return Convert.ToBase64String(mStream.ToArray());
                }
            }
        }

        private void EncryptUserSecret(string password)
        {
            UserSecret = EncryptString(UserSecret, password);
        }

        private static byte[] GenerateSalt(int sizeInBits)
        {
            var bytes = new byte[sizeInBits / 8];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return bytes;
        }

        public void GenerateUserSecret()
        {
            UserSecret = Convert.ToBase64String(GenerateSalt(256));
        }

        private static string HashPassword(string password, byte[] salt)
        {
            return Convert.ToBase64String(
                KeyDerivation.Pbkdf2(
                    password: password,
                    salt: salt,
                    prf: KeyDerivationPrf.HMACSHA1,
                    iterationCount: 10000,
                    numBytesRequested: 256 / 8
                )
            );
        }

        public bool SetPassword(string password)
        {
            var passwordSalt = GenerateSalt(256);
            var hashedPassword = HashPassword(password, passwordSalt);

            Password = hashedPassword;
            PasswordSalt = Convert.ToBase64String(passwordSalt);
            
            EncryptUserSecret(password);

            return true;
        }

        public bool VerifyPassword(string password)
        {
            var hashedPassword = HashPassword(password, Convert.FromBase64String(PasswordSalt));
            return hashedPassword == Password;
        }
    }
}