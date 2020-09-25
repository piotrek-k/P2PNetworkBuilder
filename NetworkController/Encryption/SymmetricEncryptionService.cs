using NetworkController.Interfaces;
using System;
using System.IO;
using System.Security.Cryptography;

namespace NetworkController.Encryption
{
    public class SymmetricEncryptionService
    {
        public Aes Aes { get; private set; }

        public SymmetricEncryptionService()
        {
            Aes = Aes.Create();
            Aes.Padding = PaddingMode.Zeros;
        }

        public SymmetricEncryptionService(byte[] key) : this()
        {
            Aes.Key = key;
        }

        public SymmetricEncryptionService(AesKeyContainer aesKeyContainer) : this(aesKeyContainer.Key) { }

        [Serializable]
        public class AesKeyContainer
        {
            public AesKeyContainer(SymmetricEncryptionService ses)
            {
                Aes aes = ses.Aes;
                Key = aes.Key;
            }

            public AesKeyContainer()
            {
                // for JSON deserialization
            }

            public byte[] Key { get; set; }
        }

        public AesKeyContainer ExportKeys()
        {
            return new AesKeyContainer(this);
        }

        public byte[] GetIV()
        {
            Aes.GenerateIV();
            return Aes.IV;
        }

        public byte[] Encrypt(byte[] data, byte[] IV)
        {
            if (data == null)
                return null;

            // Create an encryptor to perform the stream transform.
            ICryptoTransform encryptor = Aes.CreateEncryptor(Aes.Key, IV);

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

        public byte[] Decrypt(byte[] data, byte[] IV)
        {
            // Create a decryptor to perform the stream transform.
            ICryptoTransform decryptor = Aes.CreateDecryptor(Aes.Key, IV);

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
