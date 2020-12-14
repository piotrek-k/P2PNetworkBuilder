using System;
using System.Collections.Generic;
using System.Text;

namespace TransmissionComponent.Structures.Packers
{
    [AttributeUsage(AttributeTargets.Property)]
    public class TreatZerosLikeNullAttribute : Attribute
    {
    }
}
