using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace TransmissionComponent
{
    public interface IUdpClient
    {
        int Send(byte[] dgram, int bytes, IPEndPoint endPoint);
    }
}
