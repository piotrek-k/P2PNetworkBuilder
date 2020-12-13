using System;
using System.Collections.Generic;
using System.Net;
using TransmissionComponent.Structures.Other;

namespace TransmissionComponent
{
    public interface ITransmissionHandler
    {
        /// <summary>
        /// Executes when new message is ready to be processed.
        /// Some messages will appear as soon as they arrive, some are stopped until their turn
        /// </summary>
        Func<NewMessageEventArgs, AckStatus> NewIncomingMessage { set; }

        void SendMessageSequentially(IPEndPoint endPoint, byte[] payload, Guid source, Guid destination, Action<AckStatus> callback = null);
        void SendMessageNonSequentially(IPEndPoint endPoint, byte[] payload, Guid source, Guid destination, Action<AckStatus> callback = null);
        void SendMessageNoTracking(IPEndPoint endPoint, byte[] payload, Guid source);
        //public void SendMessageOnlyEnsureDelivered();
        //public void SendMessageAsDatagram();

        void StartListening(int port);

        int MaxPacketSize { get; set; }

        void ResetIncomingMessageCounterFor(Guid connectedDeviceGuid, int nextIdOfIncomingMessage);
        void ResetOutgoingMessageCounterFor(Guid connectedDeviceGuid, int nextIdOfOutgoingMessage);
    }
}
