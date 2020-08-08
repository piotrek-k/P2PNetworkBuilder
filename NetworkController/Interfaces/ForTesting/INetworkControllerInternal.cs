using NetworkController.UDP;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace NetworkController.Interfaces.ForTesting
{
    public interface INetworkControllerInternal : INetworkController
    {
        void SendBytes(byte[] data, IPEndPoint destination);
        IEnumerable<IExternalNodeInternal> GetNodes_Internal();
        IPAddress DeviceIPAddress { get; }
        int DevicePort { get; }
        ExternalNode AddNode(Guid id);
        void OnNetworkChangedEvent(EventArgs e);
        public Func<bool> ConnectionResetRule { get; }
    }
}
