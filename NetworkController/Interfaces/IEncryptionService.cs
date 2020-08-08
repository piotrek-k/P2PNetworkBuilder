using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkController.Interfaces
{
    public interface IEncryptionService
    {
        byte[] Encrypt(byte[] input);
        byte[] Decrypt(byte[] input);
    }
}
