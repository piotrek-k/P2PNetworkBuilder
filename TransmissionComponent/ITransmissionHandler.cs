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
        public event EventHandler<NewMessageEventArgs> NewIncomingMessage;

        public void SendMessageSequentially(IPEndPoint endPoint, int messageType, byte[] payload, Guid source, byte[] encryptionSeed, Action<AckStatus> callback = null);
        //public void SendMessageOnlyEnsureDelivered();
        //public void SendMessageAsDatagram();
    }
}
