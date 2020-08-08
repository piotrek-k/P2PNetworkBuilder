using NetworkController.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkController.Models
{
    public class ConnectionResetEventArgs : EventArgs
    {
        public IExternalNode RelatedNode { get; set; }
    }
}
