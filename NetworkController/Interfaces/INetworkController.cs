using System;
using System.Collections.Generic;
using System.Net;

namespace NetworkController.Interfaces
{
    public interface INetworkController
    {
        void StartListening(int port = 13000);
        IEnumerable<IExternalNode> GetNodes();
        Guid DeviceId { get; }
        void ConnectManually(IPEndPoint endpoint);
        event EventHandler NetworkChanged;
        void RegisterMessageTypeEnum(Type type);
        List<Type> GetMessageTypes();
    }
}
