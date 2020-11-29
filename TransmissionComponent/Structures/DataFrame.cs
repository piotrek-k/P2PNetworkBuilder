using NetworkController.DataTransferStructures;
using NetworkController.DataTransferStructures.Packers;
using System;

namespace TransmissionComponent.Structures
{
    /// <summary>
    /// Outer frame, sent over internet
    /// </summary>
    [Serializable]
    public class DataFrame : ConvertableToFixedByteStruct<DataFrame>
    {
        /// <summary>
        /// Id of sender of this frame
        /// </summary>
        [ValueToPack(1)]
        [FixedSize(16)]
        public byte[] SourceNodeId { get; set; }

        public Guid SourceNodeIdGuid
        {
            get
            {
                return new Guid(SourceNodeId);
            }
            set
            {
                SourceNodeId = value.ToByteArray();
            }
        }

        [ValueToPack(2)]
        [FixedSize(4)]
        public int MessageType { get; set; }

        [ValueToPack(3)]
        [FixedSize(4)]
        public uint RetransmissionId { get; set; }

        [ValueToPack(4)]
        [FixedSize(1)]
        public bool ExpectAcknowledge { get; set; }

        /// <summary>
        /// Initialization vector for encryption
        /// </summary>
        [ValueToPack(5)]
        [FixedSize(16)]
        [TreatZerosLikeNull]
        public byte[] IV { get; set; }

        [ValueToPack(6)]
        [FixedSize(4)]
        public int PayloadSize { get; set; }

        /// <summary>
        /// Data sent in frame
        /// </summary>
        [ValueToPack(7)]
        public byte[] Payload { get; set; }
    }
}
