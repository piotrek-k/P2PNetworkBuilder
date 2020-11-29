using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TransmissionComponent
{
    public interface IUdpClient
    {
        public Socket Client { get; set; }
        int Send(byte[] dgram, int bytes, IPEndPoint endPoint);
        public IAsyncResult BeginReceive(AsyncCallback requestCallback, object state);
        public byte[] EndReceive(IAsyncResult asyncResult, ref IPEndPoint remoteEP);
    }
}
