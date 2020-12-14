using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TransmissionComponent
{
    public interface IUdpClient
    {
        Socket Client { get; set; }
        int Send(byte[] dgram, int bytes, IPEndPoint endPoint);
        IAsyncResult BeginReceive(AsyncCallback requestCallback, object state);
        byte[] EndReceive(IAsyncResult asyncResult, ref IPEndPoint remoteEP);
    }
}
