using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace TransmissionComponent.Structures
{
    public class TrackedMessage
    {
        public byte[] Contents { get; set; }
        public object ThreadLock { get; set; } = new object();
        public IPEndPoint Endpoint { get; set; }

        public TrackedMessage(byte[] contents, IPEndPoint endpoint)
        {
            Contents = contents;
            Endpoint = endpoint;
        }
    }
}
