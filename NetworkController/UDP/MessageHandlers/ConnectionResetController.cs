using ConnectionsManager.MessageFramework;
using FrameworkPrototype;
using Microsoft.Extensions.Logging;
using NetworkController.DataTransferStructures;
using NetworkController.Interfaces.ForTesting;
using NetworkController.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkController.UDP.MessageHandlers
{
    public class ConnectionResetController : MessageController
    {
        public ConnectionResetController(ILogger logger) : base(logger)
        {

        }

        [IncomingMessage(MessageType.ResetRequest)]
        public void IncomingResetRequest(IExternalNodeInternal source, byte[] bytes)
        {
            // other Node lost keys and wants to reset connection

            var data = ResetRequest.Unpack(bytes);

            if (source.NetworkController.ConnectionResetRule(source))
            {
                _logger.LogInformation("Connection reset request accepted");

                source.Aes = null;
                source.Ses = null;
                source.IsHandshakeCompleted = false;

                source.ForceResetOutgoingMessageCounter(data.IdOfNextMessageYouSend);

                int newIncomingMessageId = new Random().Next(int.MinValue, int.MaxValue);
                source.ForceResetIncomingMessageCounter(newIncomingMessageId);

                source.InitializeConnection(newIncomingMessageId);

                return;
            }
            else
            {
                _logger.LogInformation("Connection reset revoked");
                return;
            }
        }

        /**
         * RESPONSE IS DONE VIA PUBLIC_KEY
         */
    }
}
