using Microsoft.Extensions.Logging;
using Moq;
using NetworkController.DataTransferStructures;
using NetworkController.Interfaces.ForTesting;
using NetworkController.Models;
using NetworkController.UDP.MessageHandlers;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace NetworkControllerTests.IncomingMessages
{
    public class HolePunchingResponseTest
    {
        NetworkBuildingController networkBuildingController;
        //private Mock<IExternalNodeInternal> targetNodeMock;
        private Mock<ILogger<NetworkBuildingController>> logger;
        private Mock<IExternalNodeInternal> knownExternalNode;
        private Mock<IExternalNodeInternal> newExternalNode;
        private Mock<INetworkControllerInternal> networkControllerMock;

        public HolePunchingResponseTest()
        {
            logger = new Mock<ILogger<NetworkBuildingController>>();
            networkControllerMock = new Mock<INetworkControllerInternal>();
            networkBuildingController = new NetworkBuildingController(logger.Object);
            //targetNodeMock = new Mock<IExternalNodeInternal>();

            knownExternalNode = new Mock<IExternalNodeInternal>();
            knownExternalNode.SetupAllProperties();
            Guid externalNodeMockGuid = Guid.NewGuid();
            knownExternalNode.Setup(x => x.Id).Returns(externalNodeMockGuid);
            knownExternalNode.Setup(x => x.NetworkController).Returns(networkControllerMock.Object);

            newExternalNode = new Mock<IExternalNodeInternal>();
            newExternalNode.SetupAllProperties();
            Guid newExternalNodeMockGuid = Guid.NewGuid();
            newExternalNode.Setup(x => x.Id).Returns(newExternalNodeMockGuid);
            newExternalNode.Setup(x => x.NetworkController).Returns(networkControllerMock.Object);

            networkControllerMock.Setup(x => x.GetNodes_Internal()).Returns(new List<IExternalNodeInternal>()
            {
                newExternalNode.Object,
                knownExternalNode.Object
            });
        }

        [Fact]
        public void Should_Set_State_To_Building()
        {
            // Arrange
            var data = new HolePunchingResponse()
            {
                DeviceId = newExternalNode.Object.Id
            }.PackToBytes();

            // Assert
            Assert.True(newExternalNode.Object.CurrentState == NetworkController.UDP.ExternalNode.ConnectionState.NotEstablished);

            // Act
            networkBuildingController.IncomingHolePunchingResponse(knownExternalNode.Object, data);

            // Assert
            Assert.True(newExternalNode.Object.CurrentState == NetworkController.UDP.ExternalNode.ConnectionState.Building);
        }

        [Fact]
        public void Should_Start_Sending_Pings_And_Prepare_For_Response()
        {
            // Arrange
            var data = new HolePunchingResponse()
            {
                DeviceId = newExternalNode.Object.Id
            }.PackToBytes();

            // Assert
            Assert.False(newExternalNode.Object.AfterHolePunchingResponse_WaitingForPingResponse);

            // Act
            networkBuildingController.IncomingHolePunchingResponse(knownExternalNode.Object, data);

            // Assert
            newExternalNode.Verify(x => x.SendPingSeries(), Times.Once);
            Assert.True(newExternalNode.Object.AfterHolePunchingResponse_WaitingForPingResponse);
            //newExternalNode.Verify(
            //    x => x.SendBytes(It.Is<MessageType>(x => x == MessageType.Ping), It.IsAny<byte[]>()), Times.AtLeastOnce);
        }
    }
}
