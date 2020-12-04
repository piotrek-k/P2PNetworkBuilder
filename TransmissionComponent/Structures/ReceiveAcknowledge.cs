using System;
using System.Collections.Generic;
using System.Text;
using TransmissionComponent.Structures.Packers;

namespace TransmissionComponent.Structures
{
    [Serializable]
    public class ReceiveAcknowledge : ConvertableToJSONBytes<ReceiveAcknowledge>
    {
        public int Status { get; set; }
    }
}
