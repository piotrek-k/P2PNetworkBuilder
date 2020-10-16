using System;
using System.Collections.Generic;
using System.Text;

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
        public uint ProposedStartingRetransmissionId { get; set; }
    }
}
