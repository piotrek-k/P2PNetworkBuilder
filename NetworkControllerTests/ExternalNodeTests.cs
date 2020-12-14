using Microsoft.Extensions.Logging;
using Moq;
using NetworkController.UDP;
using System;
using System.Collections.Generic;
using Xunit;
using System.Net;
using ConnectionsManager.Debugging;
using TransmissionComponent;
using NetworkController.DataTransferStructures;

namespace NetworkControllerTests
{
    public class ExternalNodeTests
    {
        Mock<NetworkController.Interfaces.ForTesting.INetworkControllerInternal> networkControllerMock;
        ILogger logger;
        ExternalNode externalNode;
        NetworkBehaviourTracker nbt;
        Mock<ITransmissionHandler> transmissionHandler;

        public ExternalNodeTests()
        {
            logger = Mock.Of<ILogger<ExternalNodeTests>>();
            networkControllerMock = new Mock<NetworkController.Interfaces.ForTesting.INetworkControllerInternal>();
            nbt = new NetworkBehaviourTracker();
            transmissionHandler = new Mock<ITransmissionHandler>();
            externalNode = new ExternalNode(new Guid(), networkControllerMock.Object, logger, nbt.NewSession(), transmissionHandler.Object);
        }

        [Fact]
        public void SendAndForget_Should_Encrypt_Data_If_SymmetricKey_Not_Null()
        {
            // Arrange
            byte[] bytesOnInput = new byte[] { 1, 2, 3 };
            byte[] bytesOnOutput = null;

            networkControllerMock.Setup(x => x.GetMessageTypes()).Returns(new List<Type>() { typeof(NetworkController.Models.MessageType) });
            externalNode.Ses = new NetworkController.Encryption.SymmetricEncryptionService();

            transmissionHandler.Setup(x => x.SendMessageNoTracking(It.IsAny<IPEndPoint>(), It.IsAny<byte[]>(), It.IsAny<Guid>()))
                .Callback<IPEndPoint, byte[], Guid>((endpoint, payload, id) =>
                {
                    bytesOnOutput = NC_DataFrame.Unpack(payload).Payload;
                });

            // Act
            externalNode.SendAndForget((int)NetworkController.Models.MessageType.Unknown, bytesOnInput);

            // Assert
            Assert.NotNull(bytesOnOutput);
            Assert.NotEqual(bytesOnInput, bytesOnOutput);
        }

        [Fact]
        public void InitializeConnection_Begins_Handshake_Process()
        {
            // Arrange
            int sentMessageType = -1;

            networkControllerMock.Setup(x => x.GetMessageTypes()).Returns(new List<Type>() { typeof(NetworkController.Models.MessageType) });

            transmissionHandler.Setup(x => x.SendMessageNoTracking(It.IsAny<IPEndPoint>(), It.IsAny<byte[]>(), It.IsAny<Guid>()))
                .Callback<IPEndPoint, byte[], Guid>((endpoint, payload, id) =>
                {
                    sentMessageType = NC_DataFrame.Unpack(payload).MessageType;
                });

            // Act
            externalNode.InitializeConnection();

            // Assert
            transmissionHandler.Verify(x => x.SendMessageNoTracking(It.IsAny<IPEndPoint>(), It.IsAny<byte[]>(), It.IsAny<Guid>()));
            Assert.True(sentMessageType == (int)NetworkController.Models.MessageType.PublicKey);
        }

        // TODO: Check if SendBytes decrypts incoming messages
    }
}
