using NetworkController.Interfaces;
using System;
using System.IO;
using System.Security.Cryptography;

namespace NetworkController.Encryption
{
    public class SymmetricEncryptionService : IEncryptionService
    {
        public Aes Aes { get; private set; }

        public SymmetricEncryptionService()
        {
            Aes = Aes.Create();
            Aes.Padding = PaddingMode.Zeros;
        }

        public SymmetricEncryptionService(byte[] key, byte[] IV) : this()
        {
            Aes.Key = key;
            Aes.IV = IV;
        }

        public SymmetricEncryptionService(AesKeyContainer aesKeyContainer) : this(aesKeyContainer.Key, aesKeyContainer.IV) { }

        [Serializable]
        public class AesKeyContainer
        {
            public AesKeyContainer(SymmetricEncryptionService ses)
            {
                Aes aes = ses.Aes;
                Key = aes.Key;
                IV = aes.IV;
            }

            public AesKeyContainer()
            {
                // for JSON deserialization
            }

            public byte[] Key { get; set; }
            public byte[] IV { get; set; }
        }

        public AesKeyContainer ExportKeys()
        {
            return new AesKeyContainer(this);
        }

        public byte[] Encrypt(byte[] data)
        {
            if (data == null)
                return null;

            // Create an encryptor to perform the stream transform.
            ICryptoTransform encryptor = Aes.CreateEncryptor(Aes.Key, Aes.IV);

            // Create the streams used for encryption.
            using (MemoryStream msEncrypt = new MemoryStream())
            {
                using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                {
                    csEncrypt.Write(data, 0, data.Length);
                    csEncrypt.FlushFinalBlock();

                    return msEncrypt.ToArray();
                }
            }
        }

        public byte[] Decrypt(byte[] data)
        {
            // Create a decryptor to perform the stream transform.
            ICryptoTransform decryptor = Aes.CreateDecryptor(Aes.Key, Aes.IV);

            // Create the streams used for decryption.
            using (MemoryStream msDecrypt = new MemoryStream(data))
            {
                using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Write))
                {
                    csDecrypt.Write(data, 0, data.Length);
                    csDecrypt.FlushFinalBlock();

                    return msDecrypt.ToArray();
                }
            }
        }
    }
}
