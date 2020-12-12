using NetworkController.Models;
using System;
using System.Net;
using TransmissionComponent.Structures.Other;
using static NetworkController.UDP.ExternalNode;

namespace NetworkController.Interfaces
{
    public interface IExternalNode
    {
        /// <summary>
        /// IP and Port used for connecting to this Node
        /// </summary>
        IPEndPoint CurrentEndpoint { get; }
        /// <summary>
        /// Send bytes to endpoint. Guaranteed to be delivered.
        /// </summary>
        /// <param name="messageType">MessageType enum</param>
        /// <param name="payload">Payload to send to Node</param>
        /// <param name="callback">Function that will be called after receiving data arrival acknowledge from another node</param>
        void SendMessageSequentially(int messageType, byte[] payload, Action<AckStatus> callback = null);
        void SendMessageNonSequentially(int messageType, byte[] payload, Action<AckStatus> callback = null);
        void SendMessageNonSequentially(int messageType, byte[] payload, IPEndPoint customEndpoint, Action<AckStatus> callback = null);
        /// <summary>
        /// Send message without tracking and delivery check. Works like UDP diagram.
        /// </summary>
        /// <param name="type">MessageType enum</param>
        /// <param name="payloadOfDataFrame"></param>
        void SendAndForget(int type, byte[] payloadOfDataFrame, IPEndPoint customEndpoint = null);
        /// <summary>
        /// Called when message that was not handled by NetworkController arrives.
        /// Can be used for handling higher-layer network messages.
        /// </summary>
        event EventHandler<BytesReceivedEventArgs> BytesReceived;
        /// <summary>
        /// Called when external node reestablishes connection. May be caused by external node reboot, or temporal connection failure.
        /// Fired after receiving new PublicKey and successfully passing ConnectionResetRule
        /// </summary>
        event EventHandler<ConnectionResetEventArgs> ConnectionReset;
        /// <summary>
        /// Id of external node
        /// </summary>
        Guid Id { get; }
        /// <summary>
        /// State of connection between this device and external node
        /// </summary>
        ConnectionState CurrentState { get; }

        /// <summary>
        /// Use for restoring security keys from permament memory
        /// </summary>
        /// <param name="actionOnFailure">action to perform when establishing connection fails, possibly due to incorrect key</param>
        void RestoreSecurityKeys(byte[] key, Action actionOnFailure = null);
        /// <summary>
        /// Use to get security keys in order to store it in permament memory
        /// </summary>
        /// <returns></returns>
        byte[] GetSecurityKeys();
        /// <summary>
        /// Sends request to other node to perform handshaking again
        /// </summary>
        void RestartConnection();
        bool IsHandshakeCompleted { get; }
        void ClearEvents();
    }
}
