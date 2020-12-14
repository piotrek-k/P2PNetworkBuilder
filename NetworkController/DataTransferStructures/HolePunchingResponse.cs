using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using TransmissionComponent.Structures.Packers;

namespace NetworkController.DataTransferStructures
{
    [Serializable]
    public class HolePunchingResponse : ConvertableToBytes<HolePunchingResponse>
    {
        public bool IsMasterNode { get; set; }

        public Guid DeviceId { get; set; }

        public string IPv4SeenExternally { get; set; }
        public int PortSeenExternally { get; set; }

        public string IPv4SeenInternally { get; set; }
        public int PortSeenInternally { get; set; }
    }
}
