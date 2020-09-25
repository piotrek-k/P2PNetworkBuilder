using NetworkController.Encryption;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using static NetworkController.Encryption.SymmetricEncryptionService;

namespace NetworkControllerTests
{
    public class EncryptionTests
    {
        [Fact]
        public void AE_Should_Sucessfully_Decrypt_Encrypted_Message()
        {
            AsymmetricEncryptionService aes = new AsymmetricEncryptionService();
            AsymmetricEncryptionService aes2 = new AsymmetricEncryptionService(aes.PublicKey);
            byte[] unencryptedBytes = { 0, 1, 2, 3, 4, 5, 6 };

            byte[] encryptedBytes = aes2.Encrypt(unencryptedBytes);
            byte[] decryptedBytes = aes.Decrypt(encryptedBytes);

            Assert.Equal(unencryptedBytes, decryptedBytes);
        }

        [Fact]
        public void SE_Should_Sucessfully_Decrypt_Encrypted_Message()
        {
            SymmetricEncryptionService ses = new SymmetricEncryptionService();
            SymmetricEncryptionService ses2 = new SymmetricEncryptionService(new AesKeyContainer(ses));
            byte[] unencryptedBytes = { 0, 1, 2, 3, 4, 5, 6 };
            byte[] newUnencryptedBytes = new byte[16];
            unencryptedBytes.CopyTo(newUnencryptedBytes, 0); // size of array must be power of 2

            var iv = ses.GetIV();

            byte[] encryptedBytes = ses.Encrypt(newUnencryptedBytes, iv);
            byte[] decryptedBytes = ses2.Decrypt(encryptedBytes, iv);

            Assert.Equal(newUnencryptedBytes, decryptedBytes);
        }
    }
}
