using System;
using System.Collections.Generic;
using System.Text;
using TransmissionComponent.Structures.Packers;

namespace NetworkController.DataTransferStructures
{
    /// <summary>
    /// Answer to ConnectionRestoreRequest
    /// </summary>
    [Serializable]
    public class ConnectionRestoreResponse : ConvertableToJSONBytes<ConnectionRestoreResponse>
    {
        /// <summary>
        /// Used for making sure that data can be decrypted
        /// </summary>
        public int IdOfNextMessageYouSendMe { get; set; }
    }
}
