using System;
using System.Collections.Generic;
using System.Text;
using TransmissionComponent.Structures.Packers;

namespace NetworkController.DataTransferStructures
{

    /// <summary>
    /// Use when connection is lost, but you've still got security keys to restore it
    /// </summary>
    [Serializable]
    public class ConnectionRestoreRequest : ConvertableToJSONBytes<ConnectionRestoreRequest>
    {
        /// <summary>
        /// Used for making sure that data can be decrypted
        /// </summary>
        public string SampleDataForEncryptionVerification { get; set; }
        public int IdOfNextMessageYouSend { get; set; }
    }
}
