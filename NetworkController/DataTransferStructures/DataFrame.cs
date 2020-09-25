using NetworkController.DataTransferStructures;
using NetworkController.Models;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace NetworkController
{
    /// <summary>
    /// Outer frame, sent over internet
    /// </summary>
    [Serializable]
    public class DataFrame : ConvertableToBytes<DataFrame>
    {
        /// <summary>
        /// Id of sender of this frame
        /// </summary>
        public Guid SourceNodeId { get; set; }

        public int MessageType { get; set; }
        public uint RetransmissionId { get; set; }
        public bool ExpectAcknowledge { get; set; }

        /// <summary>
        /// Data sent in frame
        /// </summary>
        public byte[] Payload { get; set; }

        /// <summary>
        /// Initialization vector for encryption
        /// </summary>
        public byte[] IV { get; set; }
    }
}
