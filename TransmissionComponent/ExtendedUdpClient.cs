using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using TransmissionComponent.Structures.Other;

namespace TransmissionComponent
{
    public class ExtendedUdpClient : ITransmissionHandler
    {
        public void SendMessageSequentially(IPEndPoint endPoint, int messageType, byte[] payload, Action<AckStatus> callback = null)
        {
            throw new NotImplementedException();
        }
    }
}
