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
    public class ConnectionRestoreController : MessageController
    {
        public ConnectionRestoreController(ILogger logger) : base(logger)
        {

        }

        [IncomingMessage(MessageType.ConnectionRestoreRequest)]
        public void IncomingConnectionRestoreRequest(IExternalNodeInternal source, byte[] bytes)
        {
            var data = ConnectionRestoreRequest.Unpack(bytes);
            if (!data.SampleDataForEncryptionVerification.Equals(ExternalNode.SAMPLE_ENCRYPTION_VERIFICATION_TEXT))
            {
                throw new Exception("Failed to properly decrypt message");
            }
            else
            {
                // request can be trusted

                source.IsHandshakeCompleted = false;

                source.ForceResetOutgoingMessageCounter(data.IdOfNextMessageYouSend);

                int newIncommingMessageId = new Random().Next(int.MinValue, int.MaxValue);
                source.ForceResetIncomingMessageCounter(newIncommingMessageId);

                source.SendMessageSequentially((int)MessageType.ConnectionRestoreResponse, new ConnectionRestoreResponse()
                {
                    IdOfNextMessageYouSendMe = newIncommingMessageId
                }.PackToBytes());
            }
        }

        [IncomingMessage(MessageType.ConnectionRestoreResponse)]
        public void IncomingConnectionRestoreResponse(IExternalNodeInternal source, byte[] bytes)
        {
            var data = ConnectionRestoreResponse.Unpack(bytes);

            source.ForceResetOutgoingMessageCounter(data.IdOfNextMessageYouSendMe);

            source.SendMessageSequentially((int)MessageType.AdditionalInfoRequest, null, (air_status) =>
                {
                    var ai = HandshakeController.GenerateAdditionalInfo(source);
                    source.SendMessageSequentially((int)MessageType.AdditionalInfo, ai.PackToBytes());
                });
        }
    }
}
