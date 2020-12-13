using System;
using System.Collections.Generic;
using System.Text;
using TransmissionComponent.Structures.Packers;

namespace NetworkController.DataTransferStructures
{
    [Serializable]
    public class HolePunchingRequest : ConvertableToBytes<HolePunchingRequest>
    {
        public Guid RequestedDeviceId { get; set; }
    }
}
