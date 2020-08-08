using NetworkController.Models;
using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using static NetworkController.UDP.ExternalNode;

namespace NetworkController.Interfaces
{
    public interface IExternalNode
    {
        IPEndPoint CurrentEndpoint { get; }
        void SendPing();

        /// <summary>
        /// Send bytes to endpoint. Guaranteed to be delivered.
        /// </summary>
        void SendBytes(int type, byte[] bytes, Action callback = null);
        /// <summary>
        /// Send message without tracking and delivery check. Works like UDP diagram.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="payloadOfDataFrame"></param>
        void SendAndForget(int type, byte[] payloadOfDataFrame);
        /// <summary>
        /// Called when message that was not handled by NetworkController arrives 
        /// </summary>
        event EventHandler<BytesReceivedEventArgs> BytesReceived;
        /// <summary>
        /// Called when external node reestablishes connection. May be caused by external node reboot, or temporal connection failure.
        /// </summary>
        event EventHandler<ConnectionResetEventArgs> ConnectionReset;
        /// <summary>
        /// Id of external node
        /// </summary>
        Guid Id { get; }
        bool IsActive { get; }
        public ConnectionState CurrentState { get; }
    }
}
