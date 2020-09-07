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

        /// <summary>
        /// Function that verifies whether to allow connection reset.
        /// Takes as parameter ExternalNode instance that wants to be reset
        /// </summary>
        Func<IExternalNode, bool> ConnectionResetRule { get; set; }
        /// <summary>
        /// Function that verifies whether to allow unanncounced node to start connection.
        /// Unannounced node is a node not advertised by any other known node (it
        /// announced its existence by itself)
        /// </summary>
        Func<Guid, bool> NewUnannouncedConnectionAllowanceRule { get; set; }
    }
}
