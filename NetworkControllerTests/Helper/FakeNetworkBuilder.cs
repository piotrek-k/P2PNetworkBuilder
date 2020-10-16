using ConnectionsManager.Debugging;
using Microsoft.Extensions.Logging;
using Moq;
using NetworkController;
using NetworkController.DataTransferStructures.Other;
using NetworkController.Interfaces;
using NetworkController.Interfaces.ForTesting;
using NetworkController.Models;
using NetworkController.UDP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Text;

namespace NetworkControllerTests.Helper
{
    public class FakeNetworkBuilder
    {
        public List<INetworkControllerInternal> Devices { get; set; } = new List<INetworkControllerInternal>();
        public class ConnectionInfo
        {
            public ExternalNode Node { get; set; }
            public uint Counter { get; set; }
        }
        public Dictionary<Guid, List<ConnectionInfo>> ExternalConnections { get; set; } = new Dictionary<Guid, List<ConnectionInfo>>();
        private ILogger _logger;
        private NetworkBehaviourTracker _nbt;
        private Random rnd = new Random(1);

        public FakeNetworkBuilder(ILogger logger, NetworkBehaviourTracker nbt)
        {
            _logger = logger;
            _nbt = nbt;
        }

        //public void SimulateSendingMessage(DataFrame df, INetworkControllerInternal destination, Action<AckStatus> c = null)
        //{
        //    _tm.Object.SendFrameEnsureDelivered(df, new IPEndPoint(destination.DeviceIPAddress, destination.DevicePort), c);
        //}

        //public void SimulateSendingMessage(DataFrame df, IPEndPoint destinationEndpoint, Action<AckStatus> c = null)
        //{
        //    _tm.Object.SendFrameEnsureDelivered(df, destinationEndpoint, c);
        //}

        public void HandleMessage(DataFrame df, IPEndPoint ep, Action<AckStatus> c = null)
        {
            var destDevice = Devices.FirstOrDefault(x => new IPEndPoint(x.DeviceIPAddress, x.DevicePort).Equals(ep));
            var info = ExternalConnections[destDevice.DeviceId].FirstOrDefault(x => x.Node.Id == df.SourceNodeIdGuid);

            info.Node.HandleIncomingBytes(df);
        }

        public INetworkControllerInternal GenerateFullMockOfNetworkController(Guid deviceId)
        {
            var result = new Mock<INetworkControllerInternal>();

            List<ConnectionInfo> ExternalNodesStorage;
            if (!ExternalConnections.TryGetValue(deviceId, out ExternalNodesStorage))
            {
                ExternalNodesStorage = new List<ConnectionInfo>();
                ExternalConnections.Add(deviceId, ExternalNodesStorage);
            }

            result.SetupAllProperties();

            result.Setup(x => x.DeviceId).Returns(deviceId);

            int randomNumber;
            IPEndPoint endp;
            do
            {
                randomNumber = rnd.Next(1, 255);
                endp = new IPEndPoint(IPAddress.Parse($"192.168.1.{randomNumber}"), 13000 + randomNumber);
            } while (EndpointExists(endp));

            result.Setup(x => x.DeviceIPAddress).Returns(endp.Address);
            result.Setup(x => x.DevicePort).Returns(endp.Port);
            result.Setup(x => x.DeviceEndpoint).Returns(endp);
            result.Setup(x => x.GetMessageTypes()).Returns(new List<Type>() { typeof(MessageType) });

            result.Setup(x => x.GetNodes_Internal()).Returns(ExternalNodesStorage.Select(x => x.Node));
            result.Setup(x => x.GetNodes()).Returns(ExternalNodesStorage.Cast<IExternalNode>().ToList());

            result.Setup(x => x.AddNode(It.IsAny<Guid>())).Returns((Guid guid) =>
            {
                ExternalNode newNode = null;
                if (!ExternalNodesStorage.Any(x => x.Node.Id == guid))
                {
                    FakeTransmissionManager ftm = new FakeTransmissionManager(this, _logger);
                    newNode = new ExternalNode(guid, result.Object, _logger, _nbt.NewSession(), ftm);
                    //newNode.PublicEndpoint = result.Object.DeviceEndpoint;
                    newNode.PublicEndpoint = Devices.First(x => x.DeviceId == guid).DeviceEndpoint;
                    ExternalNodesStorage.Add(new ConnectionInfo { Node = newNode, Counter = 1 });
                    return newNode;
                }
                else
                {
                    _logger.LogError("Node already present");
                    return null;
                }
            });

            Devices.Add(result.Object);

            return result.Object;
        }

        public bool EndpointExists(IPEndPoint endpoint)
        {
            return Devices.Any(x => x.DeviceIPAddress.Equals(endpoint.Address) && x.DevicePort.Equals(endpoint.Port));
        }
    }
}
