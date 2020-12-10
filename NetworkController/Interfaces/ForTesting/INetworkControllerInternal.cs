using NetworkController.Persistance;
using NetworkController.UDP;
using System;
using System.Collections.Generic;
using System.Net;
using static NetworkController.UDP.NetworkManager;

namespace NetworkController.Interfaces.ForTesting
{
    public interface INetworkControllerInternal : INetworkController
    {
        //void SendBytes(byte[] data, IPEndPoint destination);
        IEnumerable<IExternalNodeInternal> GetNodes_Internal();
        IPAddress DeviceIPAddress { get; }
        int DevicePort { get; }
        IPEndPoint DeviceEndpoint { get; }
        IExternalNodeInternal AddNode(Guid id);
        void OnNetworkChangedEvent(EventArgs e);
        void OnNodeFinishedHandshakingEvent(HandshakingFinishedEventArgs e);
        void RegisterPersistentNodeStorage(IPersistentNodeStorage storage);
    }
}
