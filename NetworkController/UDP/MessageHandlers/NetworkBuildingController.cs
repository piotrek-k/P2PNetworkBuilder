using ConnectionsManager.MessageFramework;
using FrameworkPrototype;
using Microsoft.Extensions.Logging;
using NetworkController.DataTransferStructures;
using NetworkController.Interfaces.ForTesting;
using NetworkController.Models;
using System.Linq;
using System.Net;

namespace NetworkController.UDP.MessageHandlers
{
    public class NetworkBuildingController : MessageController
    {
        public NetworkBuildingController(ILogger logger) : base(logger)
        { }

        [IncomingMessage(MessageType.HolePunchingRequest)]
        public void IncomingHolePunchingRequest(IExternalNodeInternal connectRequester, byte[] bytes)
        {
            HolePunchingRequest holePunchingRequest = HolePunchingRequest.Unpack(bytes);
            IExternalNodeInternal nodeToWhichSourceWantToConnect =
                connectRequester.NetworkController.GetNodes_Internal().FirstOrDefault(x => x.Id == holePunchingRequest.RequestedDeviceId);

            if (nodeToWhichSourceWantToConnect == null)
            {
                _logger.LogError("HPR: cannot arrange connection. No such node.");
                return;
            }

            HolePunchingResponse responseToRequester = new HolePunchingResponse
            {
                IsMasterNode = true,
                DeviceId = holePunchingRequest.RequestedDeviceId,
                IPv4SeenExternally = nodeToWhichSourceWantToConnect.PublicEndpoint?.Address.MapToIPv4().ToString(),
                PortSeenExternally = nodeToWhichSourceWantToConnect.PublicEndpoint.Port,
                IPv4SeenInternally = nodeToWhichSourceWantToConnect.ClaimedPrivateEndpoint?.Address.MapToIPv4().ToString(),
                PortSeenInternally = nodeToWhichSourceWantToConnect.ClaimedPrivateEndpoint.Port
            };

            HolePunchingResponse informationToConnectionTarget = new HolePunchingResponse
            {
                IsMasterNode = false,
                DeviceId = connectRequester.Id,
                IPv4SeenExternally = connectRequester.PublicEndpoint.Address.MapToIPv4().ToString(),
                PortSeenExternally = connectRequester.PublicEndpoint.Port,
                IPv4SeenInternally = connectRequester.ClaimedPrivateEndpoint.Address.MapToIPv4().ToString(),
                PortSeenInternally = connectRequester.ClaimedPrivateEndpoint.Port
            };

            connectRequester.SendMessageSequentially((int)MessageType.HolePunchingResponse, responseToRequester.PackToBytes());
            nodeToWhichSourceWantToConnect.SendMessageSequentially((int)MessageType.HolePunchingResponse, informationToConnectionTarget.PackToBytes());
        }

        [IncomingMessage(MessageType.HolePunchingResponse)]
        public void IncomingHolePunchingResponse(IExternalNodeInternal source, byte[] bytes)
        {
            HolePunchingResponse data = HolePunchingResponse.Unpack(bytes);

            if (data.DeviceId == source.NetworkController.DeviceId)
            {
                _logger.LogError("(HolePunchingResponse) Trying to connect to myself. Rejecting response.");
                return;
            }

            var node = source.NetworkController.GetNodes_Internal().FirstOrDefault(x => x.Id == data.DeviceId);

            if (node == null)
            {
                var newNode = source.NetworkController.AddNode(data.DeviceId);
                newNode.PublicEndpoint = new IPEndPoint(IPAddress.Parse(data.IPv4SeenExternally), data.PortSeenExternally);

                node = newNode;
            }

            node.ClaimedPrivateEndpoint = new IPEndPoint(IPAddress.Parse(data.IPv4SeenInternally), data.PortSeenInternally);

            _logger.LogTrace($"(HPR) received info about ${data.DeviceId}: " +
                $"IP1: {new IPEndPoint(IPAddress.Parse(data.IPv4SeenExternally), data.PortSeenExternally)}, IP2: {node.ClaimedPrivateEndpoint}");

            node.CurrentState = ExternalNode.ConnectionState.Building;

            if (data.IsMasterNode)
            {
                // only one side should begin connection
                node.AfterHolePunchingResponse_WaitingForPingResponse = true;
            }

            if (node.PublicEndpoint == null || node.ClaimedPrivateEndpoint == null)
            {
                _logger.LogError($"Not enough data for holepunching. Missing: {(node.PublicEndpoint == null ? "PublicEndpoint" : "")}" +
                    $" {(node.ClaimedPrivateEndpoint == null ? "ClaimedPrivateEndpoint" : "")}");
            }

            node.SendPingSeries();
        }
    }
}
