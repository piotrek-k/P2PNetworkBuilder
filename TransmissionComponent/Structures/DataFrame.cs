using System;
using TransmissionComponent.Structures.Packers;

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
        public int RetransmissionId { get; set; }

        [ValueToPack(4)]
        [FixedSize(1)]
        public byte Flags { get; set; }

        const ushort ExpAckBitwisePosition = 0;
        public bool ExpectAcknowledge
        {
            get
            {
                return (Flags & (1 << ExpAckBitwisePosition)) > 0;
            }
            set
            {
                Flags = SetBitToBoolValue(Flags, ExpAckBitwisePosition, value);
            }
        }

        const int SendSeqBitwisePosition = 1;
        public bool SendSequentially
        {
            get
            {
                return (Flags & (1 << SendSeqBitwisePosition)) > 0;
            }
            set
            {
                Flags = SetBitToBoolValue(Flags, SendSeqBitwisePosition, value);
            }
        }

        const int ReceiveAckPosition = 2;
        public bool ReceiveAck
        {
            get
            {
                return (Flags & (1 << ReceiveAckPosition)) > 0;
            }
            set
            {
                Flags = SetBitToBoolValue(Flags, ReceiveAckPosition, value);
            }
        }

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


        /*
         * Flags helpers
         */

        public static byte SetBitToBoolValue(byte value, int position, bool boolValue)
        {
            if (boolValue)
                return SetBitTo1(value, position);
            else
                return SetBitTo0(value, position);
        }

        public static byte SetBitTo1(byte value, int position)
        {
            // Set a bit at position to 1.
            return value |= (byte)(1 << position);
        }

        public static byte SetBitTo0(byte value, int position)
        {
            // Set a bit at position to 0.
            return (byte)(value & (~(1 << position)));
        }
    }
}
