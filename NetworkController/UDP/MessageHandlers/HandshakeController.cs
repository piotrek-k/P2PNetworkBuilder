using ConnectionsManager.MessageFramework;
using FrameworkPrototype;
using Microsoft.Extensions.Logging;
using NetworkController.DataTransferStructures;
using NetworkController.Encryption;
using NetworkController.Interfaces.ForTesting;
using System.Linq;
using System.Net;
using NetworkController.Models;
using System;

namespace NetworkController.UDP.MessageHandlers
{
    public class HandshakeController : MessageController
    {
        public HandshakeController(ILogger logger) : base(logger)
        {
        }

        public static AdditionalInfo GenerateAdditionalInfo(IExternalNodeInternal source)
        {
            return new AdditionalInfo()
            {
                KnownNodes = source.NetworkController.Nodes?
                    .Where(x => x.CurrentState == ExternalNode.ConnectionState.Ready).Select(x => x.Id).ToList(),
                ClaimedPrivateIPv4 = source.NetworkController.DeviceIPAddress.MapToIPv4().ToString(),
                ClaimedPrivatePort = source.NetworkController.DevicePort
            };
        }

        [IncomingMessage(MessageType.PublicKey)]
        public void IncomingPublicKey(IExternalNodeInternal source, byte[] bytes)
        {
            var data = ConnectionInitPublicKey.Unpack(bytes);

            if (source.ConnectionResetExpirationTime != null &&
                source.ConnectionResetExpirationTime > DateTimeOffset.UtcNow)
            {
                // reset request was sent by this node and exernal node sent PublicKey as response

                source.ConnectionResetExpirationTime = null;

                if (data.RespondWithThisId == null)
                {
                    throw new Exception("Resetting connection: Public key doesn't contain proposed retransmission id");
                }
                else
                {
                    source.ForceResetOutgoingMessageCounter(data.RespondWithThisId.Value);
                }
            }
            else
            {
                // it's not response to reset so it have to be connection initialization
                // if it's unexpected, reject it.

                if(source.Ses != null || source.Aes != null || source.IsHandshakeCompleted)
                {
                    throw new Exception("Illegal PublicKey request");
                }
            }

            source.Aes = new AsymmetricEncryptionService(data.RsaParams);
            source.Ses = new SymmetricEncryptionService();

            // sending private key
            var dataToSend = new HandshakeResponsePrivateKey
            {
                AesKey = source.Ses.ExportKeys()
            }.PackToBytes();
            source.SendMessageSequentially((int)MessageType.PrivateKey, dataToSend);

            // sending additional info
            dataToSend = GenerateAdditionalInfo(source).PackToBytes();
            source.SendMessageSequentially((int)MessageType.AdditionalInfo, dataToSend);
        }

        [IncomingMessage(MessageType.PrivateKey)]
        public void IncomingPrivateKey(IExternalNodeInternal source, byte[] bytes)
        {
            var data = HandshakeResponsePrivateKey.Unpack(bytes);

            source.Ses = new SymmetricEncryptionService(data.AesKey);

            var dataToSend = GenerateAdditionalInfo(source).PackToBytes();

            source.SendMessageSequentially((int)MessageType.AdditionalInfo, dataToSend);
        }

        [IncomingMessage(MessageType.AdditionalInfo)]
        public void IncomingAdditionalInfo(IExternalNodeInternal source, byte[] bytes)
        {
            var data = AdditionalInfo.Unpack(bytes);

            source.ClaimedPrivateEndpoint = new IPEndPoint(IPAddress.Parse(data.ClaimedPrivateIPv4), data.ClaimedPrivatePort);

            if (data.KnownNodes != null)
            {
                foreach (var incomingNodeId in data.KnownNodes)
                {
                    if (incomingNodeId == source.NetworkController.DeviceId)
                    {
                        continue;
                    }

                    var foundNode = source.NetworkController.Nodes.FirstOrDefault(x => x.Id == incomingNodeId);

                    if (foundNode == null)
                    {
                        source.SendMessageSequentially((int)MessageType.HolePunchingRequest, new HolePunchingRequest()
                        {
                            RequestedDeviceId = incomingNodeId
                        }.PackToBytes());
                    }
                }
            }

            source.CurrentState = ExternalNode.ConnectionState.Ready;
            source.IsHandshakeCompleted = true;
            source.ReportThatConnectionIsSetUp();
            source.NetworkController.OnNodeFinishedHandshakingEvent(new NetworkManager.HandshakingFinishedEventArgs
            {
                Node = source
            });
        }

        [IncomingMessage(MessageType.AdditionalInfoRequest)]
        public void IncomingAdditionalInfoRequest(IExternalNodeInternal source, byte[] bytes)
        {
            //var data = AdditionalInfoRequest.Unpack(bytes);

            // sending additional info
            var dataToSend = GenerateAdditionalInfo(source).PackToBytes();
            source.SendMessageSequentially((int)MessageType.AdditionalInfo, dataToSend);
        }
    }
}
