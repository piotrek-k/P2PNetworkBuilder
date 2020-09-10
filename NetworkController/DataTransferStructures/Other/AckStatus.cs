using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkController.DataTransferStructures.Other
{
    public enum AckStatus : ushort
    {
        Success = 0,
        Failure = 1
    }
}
