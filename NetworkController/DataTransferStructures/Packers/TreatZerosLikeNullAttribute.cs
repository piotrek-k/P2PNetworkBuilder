using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkController.DataTransferStructures.Packers
{
    [AttributeUsage(AttributeTargets.Property)]
    public class TreatZerosLikeNullAttribute : Attribute
    {
    }
}
