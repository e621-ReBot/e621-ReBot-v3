using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace e621_ReBot_v3.Modules
{
    internal class Module_Cryptor
    {

        internal static string Encrypt(string PlainText)
        {
            using (Aes AES = Aes.Create())
            {
                AES.Key = Encoding.ASCII.GetBytes("e621-126621-126e");
                AES.IV = new byte[] { 0, 1, 2, 3, 4, 6, 8, 12, 16, 20, 24, 32, 48, 64, 80, 96 };
                AES.Mode = CipherMode.CBC;

                ICryptoTransform Encrypter = AES.CreateEncryptor();
                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, Encrypter, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            swEncrypt.Write(PlainText);
                        }
                        string EncryptedText = Convert.ToBase64String(msEncrypt.ToArray());
                        return EncryptedText;
                    }
                }
            }
        }

        internal static string Decrypt(string EncryptedText)
        {
            using (Aes AES = Aes.Create())
            {
                AES.Key = Encoding.ASCII.GetBytes("e621-126621-126e");
                AES.IV = new byte[] { 0, 1, 2, 3, 4, 6, 8, 12, 16, 20, 24, 32, 48, 64, 80, 96 };
                AES.Mode = CipherMode.CBC;

                ICryptoTransform Decrypter = AES.CreateDecryptor();
                using (MemoryStream msDecrypt = new MemoryStream(Convert.FromBase64String(EncryptedText)))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, Decrypter, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                        {
                            string DecryptedText = srDecrypt.ReadToEnd();
                            return DecryptedText;
                        }
                    }
                }
            }
        }
    }
}