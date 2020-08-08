using NetworkController.Encryption;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkController.DataTransferStructures
{
    [Serializable]
    public class HandshakeResponsePrivateKey : ConvertableToJSONBytes<HandshakeResponsePrivateKey>
    {
        public SymmetricEncryptionService.AesKeyContainer AesKey { get; set; }
    }
}
