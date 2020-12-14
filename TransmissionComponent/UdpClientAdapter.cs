using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace TransmissionComponent
{
    public class UdpClientAdapter : UdpClient, IUdpClient
    {
        public UdpClientAdapter(int port) : base(port)
        {
        }
    }
}
