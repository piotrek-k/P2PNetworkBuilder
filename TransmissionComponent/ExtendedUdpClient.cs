using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using TransmissionComponent.Structures;
using TransmissionComponent.Structures.Other;

namespace TransmissionComponent
{
    public class ExtendedUdpClient : ITransmissionHandler
    {
        IUdpClient udpClient;

        public List<DataFrame> TrackedMessages { get; private set; } = new List<DataFrame>();

        public ExtendedUdpClient()
        {
            udpClient = new UdpClientAdapter();
        }

        public ExtendedUdpClient(IUdpClient udpClientInstance) : this()
        {
            udpClient = udpClientInstance;
        }

        public void SendMessageSequentially(IPEndPoint endPoint, int messageType, byte[] payload, Action<AckStatus> callback = null)
        {
            throw new NotImplementedException();
        }
    }
}
