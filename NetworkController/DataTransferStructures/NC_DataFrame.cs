using TransmissionComponent.Structures.Packers;

namespace NetworkController.DataTransferStructures
{
    public class NC_DataFrame : ConvertableToFixedByteStruct<NC_DataFrame>
    {
        [ValueToPack(1)]
        [FixedSize(4)]
        public int MessageType { get; set; }

        /// <summary>
        /// Initialization vector for encryption
        /// </summary>
        [ValueToPack(2)]
        [FixedSize(16)]
        [TreatZerosLikeNull]
        public byte[] IV { get; set; }

        [ValueToPack(3)]
        [FixedSize(4)]
        public int PayloadSize { get; set; }

        /// <summary>
        /// Data sent in frame
        /// </summary>
        [ValueToPack(4)]
        public byte[] Payload { get; set; }
    }
}
