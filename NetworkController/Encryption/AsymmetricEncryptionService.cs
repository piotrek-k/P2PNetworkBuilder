using NetworkController.Interfaces;
using System.Security.Cryptography;

namespace NetworkController.Encryption
{
    /// <summary>
    /// 1. Sender creates instance of this class
    /// 2. Sender sends public key to receiver
    /// 3. Receiver encrypts data with public key and sends it back
    /// 4. Sender decrypts message with private key
    /// </summary>
    public class AsymmetricEncryptionService : IEncryptionService
    {
        public RSAParameters PublicKey { get; private set; }
        public string PublicKeyString { get; private set; }
        private RSACryptoServiceProvider _rsa;

        public AsymmetricEncryptionService()
        {
            _rsa = new RSACryptoServiceProvider();
            PublicKey = _rsa.ExportParameters(false);
        }

        public AsymmetricEncryptionService(RSAParameters rsa)
        {
            _rsa = new RSACryptoServiceProvider();
            _rsa.ImportParameters(rsa);
            PublicKey = _rsa.ExportParameters(false);
        }

        public byte[] Encrypt(byte[] data)
        {
            byte[] encryptedData;

            encryptedData = _rsa.Encrypt(data, false);

            return encryptedData;
        }

        public byte[] Decrypt(byte[] data)
        {
            return _rsa.Decrypt(data, false);
        }

    }
}
