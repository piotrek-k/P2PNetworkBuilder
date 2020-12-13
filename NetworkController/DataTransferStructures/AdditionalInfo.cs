using NetworkController.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkController.DataTransferStructures
{
    [Serializable]
    public class AdditionalInfo : ConvertableToJSONBytes<AdditionalInfo>
    {
        public IEnumerable<Guid> KnownNodes { get; set; }
        public string ClaimedPrivateIPv4 { get; set; }
        public int ClaimedPrivatePort { get; set; }
    }
}
