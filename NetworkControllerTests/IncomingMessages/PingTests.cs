using Microsoft.Extensions.Logging;
using Moq;
using NetworkController.Interfaces.ForTesting;
using NetworkController.Models;
using NetworkController.UDP.MessageHandlers;
using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Text;
using Xunit;

namespace NetworkControllerTests.IncomingMessages
{
    public class PingTests
    {
        private PingController pingController;
        private Mock<IExternalNodeInternal> externalNodeMock;
        private Mock<INetworkControllerInternal> networkControllerMock;
        private Mock<ILogger<PingController>> logger;

        public PingTests()
        {
            logger = new Mock<ILogger<PingController>>();
            pingController = new PingController(logger.Object);

            externalNodeMock = new Mock<IExternalNodeInternal>();
            networkControllerMock = new Mock<INetworkControllerInternal>();

            externalNodeMock.SetupAllProperties();
        }

        [Fact]
        public void Ping_Should_Response_With_PingResponse()
        {
            // Arrange

            // Act
            pingController.IncomingPing(externalNodeMock.Object, null);

            // Assert
            externalNodeMock.Verify(x => x.SendAndForget(
                It.Is<int>(y => y == (int)MessageType.PingResponse), It.IsAny<byte[]>()),
                Times.Once);
        }

        [Fact]
        public void PingResponse_Should_Begin_Handshake_Process_If_Received_HolePunchingResponse_Earlier()
        {
            // Arrange
            externalNodeMock.Setup(x => x.AfterHolePunchingResponse_WaitingForPingResponse).Returns(true);

            // Act
            pingController.IncomingPingResponse(externalNodeMock.Object, null);

            // Assert
            externalNodeMock.Verify(x => x.InitializeConnection(),
                Times.Once);
            Assert.False(externalNodeMock.Object.AfterHolePunchingResponse_WaitingForPingResponse);
        }

        [Fact]
        public void PingResponse_Does_Not_Send_Anything_By_Default()
        {
            // Act
            pingController.IncomingPingResponse(externalNodeMock.Object, null);

            // Assert
            externalNodeMock.Verify(x => x.SendBytes(
                It.Is<int>(y => y == (int)MessageType.PingResponse), It.IsAny<byte[]>(), It.IsAny<Action>()),
                Times.Never);
        }
    }
}
