using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace NetworkController.DataTransferStructures.Packers
{
    [AttributeUsage(AttributeTargets.Property)]
    public class FixedSizeAttribute : Attribute
    {
        public FixedSizeAttribute(int sizeInBytes)
        {
            SizeInBytes = sizeInBytes;
        }

        public int SizeInBytes { get; }
    }
}
