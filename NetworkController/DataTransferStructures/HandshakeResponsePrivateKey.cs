using NetworkController.Encryption;
using System;
using System.Collections.Generic;
using System.Text;
using TransmissionComponent.Structures.Packers;

namespace NetworkController.DataTransferStructures
{
    [Serializable]
    public class HandshakeResponsePrivateKey : ConvertableToJSONBytes<HandshakeResponsePrivateKey>
    {
        public SymmetricEncryptionService.AesKeyContainer AesKey { get; set; }
    }
}
