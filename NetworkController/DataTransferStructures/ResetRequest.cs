using System;
using TransmissionComponent.Structures.Packers;

namespace NetworkController.DataTransferStructures
{
    [Serializable]
    public class ResetRequest : ConvertableToJSONBytes<ResetRequest>
    {
        public int IdOfNextMessageYouSend { get; set; }
    }
}
