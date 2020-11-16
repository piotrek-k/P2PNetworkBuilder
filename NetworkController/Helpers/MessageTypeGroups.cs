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

        public static bool CanBeSentWhenConnectionIsNotYetBuilt(int messageType)
        {
            switch (messageType)
            {
                case (int)MessageType.ReceiveAcknowledge:
                case (int)MessageType.PublicKey:
                case (int)MessageType.PrivateKey:
                case (int)MessageType.AdditionalInfo:
                case (int)MessageType.Ping:
                case (int)MessageType.PingResponse:
                case (int)MessageType.HolePunchingRequest:
                case (int)MessageType.HolePunchingResponse:
                    return true;
                default:
                    return false;
            }
        }

        public static bool DoesntRequireEncryption(int messageType)
        {
            return messageType == (int)MessageType.PublicKey ||
                messageType == (int)MessageType.Ping ||
                messageType == (int)MessageType.PingResponse ||
                messageType == (int)MessageType.ReceiveAcknowledge ||
                messageType == (int)MessageType.ResetRequest;
        }

        public static bool IsKeepaliveNoLogicRelatedMessage(int messageType)
        {
            return messageType == (int)MessageType.Ping ||
                messageType == (int)MessageType.PingResponse;
        }
    }
}
