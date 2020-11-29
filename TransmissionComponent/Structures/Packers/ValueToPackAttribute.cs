using System;
using System.Collections.Generic;
using System.Text;

namespace TransmissionComponent.Structures.Packers
{
    [AttributeUsage(AttributeTargets.Property)]
    public class ValueToPackAttribute : Attribute
    {
        public ValueToPackAttribute(int placeInSequence)
        {
            PlaceInSequence = placeInSequence;
        }

        public int PlaceInSequence { get; }
    }
}
