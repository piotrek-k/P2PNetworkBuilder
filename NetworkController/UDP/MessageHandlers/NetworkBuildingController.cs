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
        public void IncomingHolePunchingRequest(IExternalNodeInternal source, byte[] bytes)
        {
            HolePunchingRequest holePunchingRequest = HolePunchingRequest.Unpack(bytes);
            IExternalNodeInternal targetNode = source.NetworkController.GetNodes_Internal().FirstOrDefault(x=>x.Id == holePunchingRequest.RequestedDeviceId);

            HolePunchingResponse targetNodeHPR = new HolePunchingResponse
            {
                IsMasterNode = true,
                DeviceId = holePunchingRequest.RequestedDeviceId,
                IPv4SeenExternally = targetNode.PublicEndpoint?.Address.MapToIPv4().ToString(),
                PortSeenExternally = targetNode.PublicEndpoint.Port,
                IPv4SeenInternally = targetNode.ClaimedPrivateEndpoint?.Address.MapToIPv4().ToString(),
                PortSeenInternally = targetNode.ClaimedPrivateEndpoint.Port
            };

            HolePunchingResponse sourceNodeHPR = new HolePunchingResponse
            {
                IsMasterNode = false,
                DeviceId = holePunchingRequest.RequestedDeviceId,
                IPv4SeenExternally = source.PublicEndpoint.Address.MapToIPv4().ToString(),
                PortSeenExternally = source.PublicEndpoint.Port,
                IPv4SeenInternally = source.ClaimedPrivateEndpoint.Address.MapToIPv4().ToString(),
                PortSeenInternally = source.ClaimedPrivateEndpoint.Port
            };

            source.SendBytes((int)MessageType.HolePunchingResponse, targetNodeHPR.PackToBytes());
            targetNode.SendBytes((int)MessageType.HolePunchingResponse, sourceNodeHPR.PackToBytes());
        }

        [IncomingMessage(MessageType.HolePunchingResponse)]
        public void IncomingHolePunchingResponse(IExternalNodeInternal source, byte[] bytes)
        {
            HolePunchingResponse data = HolePunchingResponse.Unpack(bytes);

            if(data.DeviceId == source.NetworkController.DeviceId)
            {
                _logger.LogError("Trying to connect to myself");
                return;
            }

            var node = source.NetworkController.GetNodes_Internal().FirstOrDefault(x => x.Id == data.DeviceId);

            if(node == null)
            {
                //_logger.LogError(new EventId((int)LoggerEventIds.NodeNotFound), "Node not found");
                var newNode = source.NetworkController.AddNode(data.DeviceId);
                newNode.PublicEndpoint = new IPEndPoint(IPAddress.Parse(data.IPv4SeenExternally), data.PortSeenExternally);
                newNode.ClaimedPrivateEndpoint = new IPEndPoint(IPAddress.Parse(data.IPv4SeenInternally), data.PortSeenInternally);

                node = newNode;
            }

            node.CurrentState = ExternalNode.ConnectionState.Building;
            node.AfterHolePunchingResponse_WaitingForPingResponse = true;

            if(node.PublicEndpoint == null || node.ClaimedPrivateEndpoint == null)
            {
                _logger.LogError("Not enough data for holepunching");
            }

            node.SendPingSeries();
        }
    }
}
