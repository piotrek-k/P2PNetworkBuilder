using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkController.DataTransferStructures
{
    [Serializable]
    public class ResetRequest : ConvertableToJSONBytes<ResetRequest>
    {
        public int IdOfNextMessageYouSend { get; set; }
    }
}
