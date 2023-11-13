using System;
using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace SecretDemo
{
    public class LocalEncryption
    {
        private static string _salt = "ED4247B9-8EEA-48EE-B284-8A61B6822185";
        private static byte[] _key = null;
        private static byte[] GetKey()
        {
            if (_key == null)
            {
                var managementObjectSearcher = new ManagementObjectSearcher("Select SerialNumber From Win32_BaseBoard");
                foreach (var managementObject in managementObjectSearcher.Get())
                {
                    _key = Sha512EncryptByte($"{managementObject["SerialNumber"]}{_salt}").Take(32).ToArray();
                    break;
                }
            }
            return _key;
        }

        private static byte[] _iv = null;
        private static byte[] GetIV()
        {
            if (_iv == null)
            {
                var managementObjectSearcher = new ManagementObjectSearcher("Select ProcessorID From Win32_processor");
                foreach (var managementObject in managementObjectSearcher.Get())
                {
                    _iv = Sha512EncryptByte($"{managementObject["ProcessorID"]}{_salt}").Take(16).ToArray();
                    break;
                }
            }
            return _iv;
        }

        private static byte[] Sha512EncryptByte(string plainText)
        {
            using (var sha512 = new SHA512Managed())
            {
                return sha512.ComputeHash(UTF8Encoding.UTF8.GetBytes(plainText));
            }
        }

        public static string AesEncrypt(string plainText)
        {
            if (plainText == null)
                return string.Empty;
            byte[] encrypted;
            using (var aes = Aes.Create())
            {

                var encryptor = aes.CreateEncryptor(GetKey(), GetIV());
                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            swEncrypt.Write(plainText);
                        }
                        encrypted = msEncrypt.ToArray();
                    }
                }
            }
            return Convert.ToHexString(encrypted);
        }

        public static string AesDecrypt(string encrpyted)
        {
            if (encrpyted == null)
                return string.Empty;
            using (var aes = Aes.Create())
            {
                var decryptor = aes.CreateDecryptor(GetKey(), GetIV());
                using (MemoryStream msDecrypt = new MemoryStream(Convert.FromHexString(encrpyted)))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                        {
                            return srDecrypt.ReadToEnd();
                        }
                    }
                }
            }
        }
    }
}
