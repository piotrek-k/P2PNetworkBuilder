using Microsoft.Extensions.Logging;
using Moq;
using NetworkController.DataTransferStructures;
using NetworkController.Encryption;
using NetworkController.Interfaces.ForTesting;
using NetworkController.Models;
using NetworkController.UDP;
using NetworkController.UDP.MessageHandlers;
using System;
using Xunit;
using System.Linq;
using NetworkControllerTests.Helper;
using System.Net;
using System.Collections.Generic;
using NetworkController.Interfaces;
using NetworkController;
using ConnectionsManager.Debugging;
using NetworkController.DataTransferStructures.Other;

namespace NetworkControllerTests.IncomingMessages
{
    public class HandshakingControllerTest
    {
        Mock<INetworkControllerInternal> networkControllerMock;
        Mock<IExternalNodeInternal> externalNodeMock;
        Mock<ITransmissionManager> transmissionManagerMock;
        private Mock<ILogger<HandshakingControllerTest>> logger;
        private Mock<ILogger<HandshakingControllerTest>> logger2;
        HandshakeController handshakeControllerMock;
        NetworkBehaviourTracker nbt;

        public HandshakingControllerTest()
        {
            logger = new Mock<ILogger<HandshakingControllerTest>>();
            logger2 = new Mock<ILogger<HandshakingControllerTest>>();

            nbt = new NetworkBehaviourTracker();

            networkControllerMock = new Mock<INetworkControllerInternal>();
            networkControllerMock.SetupAllProperties();
            networkControllerMock.Setup(x => x.DevicePort).Returns(13000);
            networkControllerMock.Setup(x => x.DeviceIPAddress).Returns(IPAddress.Parse("192.168.1.1"));
            networkControllerMock.Setup(x => x.GetMessageTypes()).Returns(new List<Type>() { typeof(NetworkController.Models.MessageType) });
            var nodes = new List<IExternalNode> {
                new ExternalNode(new Guid(), networkControllerMock.Object, logger.Object, nbt.NewSession()),
                new ExternalNode(new Guid(), networkControllerMock.Object, logger.Object, nbt.NewSession()),
                new ExternalNode(new Guid(), networkControllerMock.Object, logger.Object, nbt.NewSession())
            };
            nodes.ForEach(x => ((ExternalNode)x).CurrentState = ExternalNode.ConnectionState.Ready);
            networkControllerMock.Setup(x => x.GetNodes()).Returns(nodes);

            externalNodeMock = new Mock<IExternalNodeInternal>();
            externalNodeMock.SetupAllProperties();
            externalNodeMock.Setup(x => x.NetworkController).Returns(networkControllerMock.Object);

            handshakeControllerMock = new HandshakeController(logger.Object);
        }

        [Fact]
        public void Given_Public_Key_Should_Send_Private_Key()
        {
            // Arrange
            var aes = new AsymmetricEncryptionService();
            var incomingKey = new ConnectionInitPublicKey
            {
                RsaParams = aes.PublicKey
            }.PackToBytes();
            networkControllerMock.Setup(x => x.GetNodes()).Returns(new List<IExternalNode>()
            {
                 new ExternalNode(new Guid(), networkControllerMock.Object, logger.Object, nbt.NewSession())
            });

            // Act
            handshakeControllerMock.IncomingPublicKey(externalNodeMock.Object, incomingKey);

            // Assert
            externalNodeMock.Verify(x => x.SendBytes(
                It.Is<int>(x => x == (int)MessageType.PrivateKey),
                It.IsAny<byte[]>(), It.IsAny<Action<AckStatus>>()), Times.Once);
        }

        [Fact]
        public void Given_Private_Key_Should_Transmit_Additional_Info()
        {
            // Arrange
            var incomingKey = new HandshakeResponsePrivateKey
            {
                AesKey = new SymmetricEncryptionService.AesKeyContainer(
                    new SymmetricEncryptionService())
            }.PackToBytes();
            networkControllerMock.Setup(x => x.GetNodes()).Returns(new List<IExternalNode>()
            {
                 new ExternalNode(new Guid(), networkControllerMock.Object, logger.Object, nbt.NewSession())
            });

            // Act
            handshakeControllerMock.IncomingPrivateKey(externalNodeMock.Object, incomingKey);

            // Assert
            externalNodeMock.Verify(x => x.SendBytes(
                It.Is<int>(x => x == (int)MessageType.AdditionalInfo),
                It.IsAny<byte[]>(), It.IsAny<Action<AckStatus>>()), Times.Once);
        }

        [Fact]
        public void Additional_Info_Should_Contain_All_Ids_Of_Network_Members()
        {
            // Arrange
            var incomingKey = new HandshakeResponsePrivateKey
            {
                AesKey = new SymmetricEncryptionService.AesKeyContainer(
                    new SymmetricEncryptionService())
            }.PackToBytes();

            var nodes = new List<IExternalNode> {
                new ExternalNode(new Guid(), networkControllerMock.Object, logger.Object, nbt.NewSession()),
                new ExternalNode(new Guid(), networkControllerMock.Object, logger.Object, nbt.NewSession()),
                new ExternalNode(new Guid(), networkControllerMock.Object, logger.Object, nbt.NewSession())
            };
            nodes.ForEach(x => ((ExternalNode)x).CurrentState = ExternalNode.ConnectionState.Ready);

            networkControllerMock.Setup(x => x.GetNodes()).Returns(nodes);

            AdditionalInfo responseBeingSent = null;
            externalNodeMock.Setup(x => x.SendBytes(It.IsAny<int>(), It.IsAny<byte[]>(), It.IsAny<Action<AckStatus>>()))
               .Callback<int, byte[], Action<AckStatus>>((mt, bytes, c) => responseBeingSent = AdditionalInfo.Unpack(bytes));

            // Act
            handshakeControllerMock.IncomingPrivateKey(externalNodeMock.Object, incomingKey);

            // Assert
            Assert.Equal(3, responseBeingSent.KnownNodes.Count());
        }

        [Fact]
        public void Given_AdditionalInfo_Should_Start_Connecting_To_Unknown_Hosts()
        {
            // Arrange
            var alreadyKnownGuid = Guid.NewGuid();
            var transmissionManagerMock = new Mock<ITransmissionManager>();
            ExternalNode alreadyKnownNode = new ExternalNode(
                alreadyKnownGuid, networkControllerMock.Object, logger.Object, nbt.NewSession(), transmissionManagerMock.Object);
            alreadyKnownNode.RestoreSecurityKeys(new byte[16]);
            networkControllerMock.Setup(x => x.GetNodes()).Returns(new List<IExternalNode> {
                alreadyKnownNode
            });
            var incomingAI = new AdditionalInfo
            {
                KnownNodes = new List<Guid>() { Guid.NewGuid(), Guid.NewGuid(), alreadyKnownGuid },
                ClaimedPrivateIPv4 = "192.168.1.1",
                ClaimedPrivatePort = 13000
            };

            // Act
            handshakeControllerMock.IncomingAdditionalInfo(alreadyKnownNode, incomingAI.PackToBytes());

            // Assert
            transmissionManagerMock.Verify(x => x.SendFrameEnsureDelivered(
                It.Is<DataFrame>(y => y.MessageType == (int)MessageType.HolePunchingRequest),
                It.IsAny<IPEndPoint>(), It.IsAny<Action<AckStatus>>()),
                Times.Exactly(2));
        }

        [Fact]
        public void PerformFullHandshaking_Test()
        {
            logger.MockLog(LogLevel.Warning);
            logger2.MockLog(LogLevel.Warning);

            var networkControllerMockOne = new Mock<INetworkControllerInternal>();
            var networkControllerMockTwo = new Mock<INetworkControllerInternal>();
            var transmissionManagerMockOne = new Mock<ITransmissionManager>();
            var transmissionManagerMockTwo = new Mock<ITransmissionManager>();
            ExternalNode nodeOne = new ExternalNode(new Guid(), networkControllerMockOne.Object, logger.Object, nbt.NewSession(), transmissionManagerMockOne.Object);
            ExternalNode nodeTwo = new ExternalNode(new Guid(), networkControllerMockTwo.Object, logger2.Object, nbt.NewSession(), transmissionManagerMockTwo.Object);
            networkControllerMockOne.Setup(x => x.DeviceIPAddress).Returns(IPAddress.Parse("192.168.1.12"));
            networkControllerMockOne.Setup(x => x.DevicePort).Returns(13000);
            networkControllerMockOne.Setup(x => x.GetMessageTypes()).Returns(new List<Type>() { typeof(MessageType) });
            networkControllerMockTwo.Setup(x => x.DeviceIPAddress).Returns(IPAddress.Parse("192.168.1.13"));
            networkControllerMockTwo.Setup(x => x.DevicePort).Returns(13001);
            networkControllerMockTwo.Setup(x => x.GetMessageTypes()).Returns(new List<Type>() { typeof(MessageType) });

            Assert.Null(nodeOne.Ses);
            Assert.Null(nodeTwo.Ses);
            Assert.Null(nodeOne.PublicEndpoint);
            Assert.Null(nodeOne.ClaimedPrivateEndpoint);
            Assert.Null(nodeTwo.PublicEndpoint);
            Assert.Null(nodeTwo.ClaimedPrivateEndpoint);

            EnvironmentPreparation.PerformFullHandshaking(transmissionManagerMockOne, transmissionManagerMockTwo, nodeOne, nodeTwo);

            Assert.NotNull(nodeOne.Ses);
            Assert.NotNull(nodeTwo.Ses);
            //Assert.NotNull(nodeOne.PublicEndpoint);
            Assert.True(nodeOne.ClaimedPrivateEndpoint.Address.Equals(IPAddress.Parse("192.168.1.13")));
            Assert.Equal(13001, nodeOne.ClaimedPrivateEndpoint.Port);
            //Assert.NotNull(nodeTwo.PublicEndpoint);
            Assert.True(nodeTwo.ClaimedPrivateEndpoint.Address.Equals(IPAddress.Parse("192.168.1.12")));
            Assert.Equal(13000, nodeTwo.ClaimedPrivateEndpoint.Port);
            Assert.Equal(nodeOne.Ses.Aes.Key, nodeTwo.Ses.Aes.Key);

            Assert.Equal(ExternalNode.ConnectionState.Ready, nodeOne.CurrentState);
            Assert.Equal(ExternalNode.ConnectionState.Ready, nodeTwo.CurrentState);

            logger.Verify(LogLevel.Trace, LoggerEventIds.DataUnencrypted, Times.Exactly(3));
            logger2.Verify(LogLevel.Trace, LoggerEventIds.DataUnencrypted, Times.Exactly(2));
        }
    }
}
