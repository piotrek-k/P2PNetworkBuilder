using Microsoft.Extensions.Logging;
using Moq;
using NetworkController.DataTransferStructures;
using NetworkController.DataTransferStructures.Other;
using NetworkController.Interfaces;
using NetworkController.Interfaces.ForTesting;
using NetworkController.Models;
using NetworkController.UDP;
using NetworkController.UDP.MessageHandlers;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Xunit;

namespace NetworkControllerTests.IncomingMessages
{
    public class HolePunchingRequestTest
    {
        NetworkBuildingController networkBuildingController;
        private Mock<IExternalNodeInternal> alreadyKnownNodeMock;
        private Mock<ILogger<NetworkBuildingController>> logger;
        private Mock<IExternalNodeInternal> externalNodeMock;
        private Mock<INetworkControllerInternal> networkControllerMock;
        private HolePunchingResponse responseToExternalNode;
        private HolePunchingResponse responseToTargetNode;

        public HolePunchingRequestTest()
        {
            logger = new Mock<ILogger<NetworkBuildingController>>();
            externalNodeMock = new Mock<IExternalNodeInternal>();
            networkControllerMock = new Mock<INetworkControllerInternal>();
            networkBuildingController = new NetworkBuildingController(logger.Object);
            alreadyKnownNodeMock = new Mock<IExternalNodeInternal>();

            // Setting up node 2 (node to which node 1 wants to connect)
            alreadyKnownNodeMock = new Mock<IExternalNodeInternal>();
            alreadyKnownNodeMock.Setup(x => x.Id).Returns(Guid.NewGuid());
            alreadyKnownNodeMock.Setup(x => x.PublicEndpoint).Returns(IPEndPoint.Parse("7.7.7.7:7777"));
            alreadyKnownNodeMock.Setup(x => x.ClaimedPrivateEndpoint).Returns(IPEndPoint.Parse("127.0.0.1:13000"));

            // Setting up node 1 (source of incoming message)
            externalNodeMock.Setup(x => x.Id).Returns(Guid.NewGuid());
            externalNodeMock.Setup(x => x.NetworkController).Returns(networkControllerMock.Object);
            externalNodeMock.Setup(x => x.PublicEndpoint).Returns(IPEndPoint.Parse("8.8.8.8:8888"));
            externalNodeMock.Setup(x => x.ClaimedPrivateEndpoint).Returns(IPEndPoint.Parse("192.168.1.1:13000"));

            // Extracting sent data
            externalNodeMock.Setup(x => x.SendBytes(It.IsAny<int>(), It.IsAny<byte[]>(), It.IsAny<Action<AckStatus>>()))
               .Callback<int, byte[], Action<AckStatus>>((mt, bytes, c) => responseToExternalNode = HolePunchingResponse.Unpack(bytes));
            alreadyKnownNodeMock.Setup(x => x.SendBytes(It.IsAny<int>(), It.IsAny<byte[]>(), It.IsAny<Action<AckStatus>>()))
               .Callback<int, byte[], Action<AckStatus>>((mt, bytes, c) => responseToTargetNode = HolePunchingResponse.Unpack(bytes));

            // Populating NetworkController
            networkControllerMock.Setup(x => x.GetNodes_Internal()).Returns(new List<IExternalNodeInternal> {
                alreadyKnownNodeMock.Object
            });
            networkControllerMock.Setup(x => x.Nodes).Returns(new List<IExternalNode> {
                alreadyKnownNodeMock.Object
            });
        }

        [Fact]
        public void Should_Respond_With_Two_HolePunchingResponses()
        {
            // Arrange
            Guid requestedDeviceId = alreadyKnownNodeMock.Object.Id;
            var data = new HolePunchingRequest()
            {
                RequestedDeviceId = requestedDeviceId
            }.PackToBytes();

            // Act
            networkBuildingController.IncomingHolePunchingRequest(externalNodeMock.Object, data);

            // Assert
            externalNodeMock.Verify(x => x.SendBytes(
                It.Is<int>(x => x == (int)MessageType.HolePunchingResponse),
                It.IsAny<byte[]>(), It.IsAny<Action<AckStatus>>()), Times.Once);
            alreadyKnownNodeMock.Verify(x => x.SendBytes(
                It.Is<int>(x => x == (int)MessageType.HolePunchingResponse),
                It.IsAny<byte[]>(), It.IsAny<Action<AckStatus>>()), Times.Once);
        }

        [Fact]
        public void Should_Send_One_Master_And_One_Slave_Response()
        {
            // Arrange
            Guid requestedDeviceId = alreadyKnownNodeMock.Object.Id;
            var data = new HolePunchingRequest()
            {
                RequestedDeviceId = requestedDeviceId
            }.PackToBytes();

            // Act
            networkBuildingController.IncomingHolePunchingRequest(externalNodeMock.Object, data);

            // Assert
            Assert.True(responseToExternalNode.IsMasterNode != responseToTargetNode.IsMasterNode);
        }

        [Fact]
        public void Should_Contain_Proper_Device_Id()
        {
            // Arrange
            Guid requestedDeviceId = alreadyKnownNodeMock.Object.Id;
            var data = new HolePunchingRequest()
            {
                RequestedDeviceId = requestedDeviceId
            }.PackToBytes();

            // Act
            networkBuildingController.IncomingHolePunchingRequest(externalNodeMock.Object, data);

            // Assert
            Assert.False(alreadyKnownNodeMock.Object.Id == Guid.Empty);
            Assert.False(externalNodeMock.Object.Id == Guid.Empty);
            Assert.True(responseToExternalNode.DeviceId.Equals(alreadyKnownNodeMock.Object.Id));
            Assert.True(responseToTargetNode.DeviceId.Equals(externalNodeMock.Object.Id));
        }
    }
}
