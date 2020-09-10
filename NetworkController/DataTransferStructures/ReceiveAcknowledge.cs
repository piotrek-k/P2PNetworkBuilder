using NetworkController.DataTransferStructures.Other;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkController.DataTransferStructures
{
    [Serializable]
    public class ReceiveAcknowledge : ConvertableToJSONBytes<ReceiveAcknowledge>
    {
        public AckStatus Status { get; set; }
    }
}
