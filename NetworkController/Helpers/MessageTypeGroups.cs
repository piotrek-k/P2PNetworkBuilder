using NetworkController.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkController.Helpers
{
    public static class MessageTypeGroups
    {
        public static bool IsHandshakeRelatedAndShouldntBeDoneMoreThanOnce(int messageType)
        {
            return messageType == (int)MessageType.PublicKey ||
                messageType == (int)MessageType.PrivateKey ||
                messageType == (int)MessageType.AdditionalInfo;
        }

        public static bool DoesntRequireEncryption(int messageType)
        {
            return messageType == (int)MessageType.PublicKey ||
                messageType == (int)MessageType.Ping ||
                messageType == (int)MessageType.PingResponse ||
                messageType == (int)MessageType.ReceiveAcknowledge;
        }

        public static bool IsKeepaliveNoLogicRelatedMessage(int messageType)
        {
            return messageType == (int)MessageType.Ping ||
                messageType == (int)MessageType.PingResponse;
        }
    }
}
