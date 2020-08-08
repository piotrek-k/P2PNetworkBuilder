using NetworkController.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkController.Models
{
    public class BytesReceivedEventArgs : EventArgs
    {
        public IExternalNode Sender { get; set; }
        public int MessageType { get; set; }
        public byte[] Payload { get; set; }
    }
}
