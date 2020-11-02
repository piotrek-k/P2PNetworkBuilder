using System;
using System.Collections.Generic;
using System.Net;
using static NetworkController.UDP.NetworkManager;

namespace NetworkController.Interfaces
{
    public interface INetworkController
    {
        void StartListening(int port = 13000);
        IEnumerable<IExternalNode> Nodes { get; }
        List<Guid> Blacklist { get; }
        Guid DeviceId { get; set; }
        IExternalNode ConnectManually(IPEndPoint endpoint, bool initializeConnection = true, Guid? knownId = null);

        void RestorePreviousSessionFromStorage();

        /// <summary>
        /// Registers enums storing possible message ids
        /// </summary>
        /// <param name="type"></param>
        void RegisterMessageTypeEnum(Type type);
        List<Type> GetMessageTypes();

        /// <summary>
        /// Event fired when any known connection changes its current state.
        /// This includes establishing new connections and losing
        /// or suspeding existing ones
        /// </summary>
        event EventHandler NetworkChanged;
        /// <summary>
        /// Event fired when new node appears in network.
        /// Happens before handshaking finishes.
        /// </summary>
        event EventHandler NodeAdded;
        /// <summary>
        /// Event fired when node finished handshaking (and it's ready for data exchange)
        /// </summary>
        event EventHandler<HandshakingFinishedEventArgs> NodeFinishedHandshaking;

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

        /// <summary>
        /// Error will be thrown if any UDP packet sent by this device exceeds that value
        /// </summary>
        int MaxPacketSize { get; }
    }
}
