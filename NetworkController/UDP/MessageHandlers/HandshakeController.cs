using ConnectionsManager.MessageFramework;
using FrameworkPrototype;
using Microsoft.Extensions.Logging;
using NetworkController.DataTransferStructures;
using NetworkController.Encryption;
using NetworkController.Interfaces.ForTesting;
using System.Linq;
using System.Net;
using NetworkController.Models;

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
                KnownNodes = source.NetworkController.GetNodes()?
                    .Where(x => x.CurrentState == ExternalNode.ConnectionState.Ready).Select(x => x.Id).ToList(),
                ClaimedPrivateIPv4 = source.NetworkController.DeviceIPAddress.MapToIPv4().ToString(),
                ClaimedPrivatePort = source.NetworkController.DevicePort
            };
        }

        [IncomingMessage(MessageType.PublicKey)]
        public void IncomingPublicKey(IExternalNodeInternal source, byte[] bytes)
        {
            //if(source.CurrentState != ExternalNode.ConnectionState.NotEstablished &&
            //    source.CurrentState != ExternalNode.ConnectionState.Failed)
            //{
            //    _logger.LogError("Rejected public key. State is incorrect");
            //    return;
            //}

            var data = ConnectionInitPublicKey.Unpack(bytes);
            source.Aes = new AsymmetricEncryptionService(data.RsaParams);
            source.Ses = new SymmetricEncryptionService();

            // sending private key
            var dataToSend = new HandshakeResponsePrivateKey
            {
                AesKey = source.Ses.ExportKeys()
            }.PackToBytes();
            source.SendBytes((int)MessageType.PrivateKey, dataToSend);

            // sending additional info
            dataToSend = GenerateAdditionalInfo(source).PackToBytes();
            source.SendBytes((int)MessageType.AdditionalInfo, dataToSend);
        }

        [IncomingMessage(MessageType.PrivateKey)]
        public void IncomingPrivateKey(IExternalNodeInternal source, byte[] bytes)
        {
            var data = HandshakeResponsePrivateKey.Unpack(bytes);

            source.Ses = new SymmetricEncryptionService(data.AesKey);

            var dataToSend = GenerateAdditionalInfo(source).PackToBytes();

            source.SendBytes((int)MessageType.AdditionalInfo, dataToSend);
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

                    var foundNode = source.NetworkController.GetNodes().FirstOrDefault(x => x.Id == incomingNodeId);

                    if (foundNode == null)
                    {
                        source.SendBytes((int)MessageType.HolePunchingRequest, new HolePunchingRequest()
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
            source.SendBytes((int)MessageType.AdditionalInfo, dataToSend);
        }
    }
}
