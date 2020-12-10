using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using TransmissionComponent.Structures.Other;

namespace TransmissionComponent.Structures
{
    public class TrackedMessage
    {
        public byte[] Contents { get; set; }
        public object ThreadLock { get; set; } = new object();
        public IPEndPoint Endpoint { get; set; }
        public Action<AckStatus> Callback { get; set; }

        public TrackedMessage(byte[] contents, IPEndPoint endpoint, Action<AckStatus> callback = null)
        {
            Contents = contents;
            Endpoint = endpoint;
            Callback = callback;
        }
    }
}
