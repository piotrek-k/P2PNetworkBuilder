using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.ObjectModel;
using Moq;
using NetworkController.UDP;
using NetworkController.Models;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using System.Net;
using NetworkController;
using ConnectionsManager.Debugging;

namespace NetworkControllerTests
{
    public class ExternalNodeTests
    {
        Mock<NetworkController.Interfaces.ForTesting.INetworkControllerInternal> networkControllerMock;
        ILogger logger;
        ExternalNode externalNode;
        NetworkBehaviourTracker nbt;

        public ExternalNodeTests()
        {
            logger = Mock.Of<ILogger<ExternalNodeTests>>();
            networkControllerMock = new Mock<NetworkController.Interfaces.ForTesting.INetworkControllerInternal>();
            nbt = new NetworkBehaviourTracker();
            externalNode = new ExternalNode(new Guid(), networkControllerMock.Object, logger, nbt.NewSession());
        }

        [Fact]
        public void SendBytes_Should_Encrypt_Data_If_Ses_Not_Null()
        {
            // Arrange
            byte[] bytesOnInput = new byte[] { 1, 2, 3 };
            byte[] bytesOnOutput = null;
            DataFrame dataFrameOnOutput = null;
            networkControllerMock.Setup(x => x.SendBytes(It.IsAny<byte[]>(), It.IsAny<IPEndPoint>()))
                .Callback<byte[], IPEndPoint>((bytes, endpoint) => dataFrameOnOutput = DataFrame.Unpack(bytes));
            networkControllerMock.Setup(x => x.GetMessageTypes()).Returns(new List<Type>() { typeof(NetworkController.Models.MessageType) });
            externalNode.Ses = new NetworkController.Encryption.SymmetricEncryptionService();

            // Act
            externalNode.SendAndForget((int)NetworkController.Models.MessageType.Unknown, bytesOnInput);
            bytesOnOutput = dataFrameOnOutput.Payload;

            // Assert
            Assert.NotNull(bytesOnOutput);
            Assert.NotEqual(bytesOnInput, bytesOnOutput);
        }

        [Fact]
        public void InitializeConnection_Begins_Handshake_Process()
        {
            // Arrange
            DataFrame dataFrameOnOutput = null;
            networkControllerMock.Setup(x => x.SendBytes(It.IsAny<byte[]>(), It.IsAny<IPEndPoint>()))
                .Callback<byte[], IPEndPoint>((bytes, endpoint) => dataFrameOnOutput = DataFrame.Unpack(bytes));
            networkControllerMock.Setup(x => x.GetMessageTypes()).Returns(new List<Type>() { typeof(NetworkController.Models.MessageType) });

            // Act
            externalNode.InitializeConnection();

            // Assert
            networkControllerMock.Verify(x => x.SendBytes(It.IsAny<byte[]>(), It.IsAny<IPEndPoint>()));
            Assert.True(dataFrameOnOutput.MessageType == (int)NetworkController.Models.MessageType.PublicKey);
        }

        // TODO: Check if SendBytes decrypts incoming messages
    }
}
