using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkController.DataTransferStructures
{
    [Serializable]
    public class HolePunchingRequest : ConvertableToBytes<HolePunchingRequest>
    {
        public Guid RequestedDeviceId { get; set; }
    }
}
