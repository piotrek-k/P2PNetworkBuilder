﻿using System;
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
        public Func<NewMessageEventArgs, AckStatus> NewIncomingMessage { set; }

        public void SendMessageSequentially(IPEndPoint endPoint, byte[] payload, Guid source, Action<AckStatus> callback = null);
        //public void SendMessageOnlyEnsureDelivered();
        //public void SendMessageAsDatagram();
    }
}
