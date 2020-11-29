using Moq;
using System;
using System.Linq;
using System.Net;
using TransmissionComponent;
using TransmissionComponent.Structures;
using TransmissionComponent.Structures.Other;
using Xunit;

namespace TransmissionComponentTests
{
    public class ExtendedUdpClientTests
    {
        [Fact]
        public void SendMessageSequentiallyShould_PackMessage_PassItToUdpClient_StartTracking()
        {
            // Arrange
            Mock<IUdpClient> udpClientMock = new Mock<IUdpClient>();
            ExtendedUdpClient extendedUdpClient = new ExtendedUdpClient(udpClientMock.Object);
            Action<AckStatus> callback = (status) => { };

            byte[] receivedData = null;
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 13000);
            int messageType = 1;
            byte[] sentData = new byte[] { 1, 2, 3, 4 };
            int previousTrackMessagesSize = extendedUdpClient.TrackedMessages.Count();

            udpClientMock.Setup(x => x.Send(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<IPEndPoint>()))
                .Callback<byte[], int, IPEndPoint>((dgram, bytes, endpoint) =>
                {
                    receivedData = dgram;
                });

            // Act 
            extendedUdpClient.SendMessageSequentially(
                endpoint, messageType, sentData, callback);

            // Assert
            udpClientMock
                .Verify(x => x.Send(It.IsAny<byte[]>(), It.IsAny<int>(), It.Is<IPEndPoint>(x => x == endpoint)),
                    Times.Once);

            DataFrame result = DataFrame.Unpack(receivedData);
            Assert.True(result.ExpectAcknowledge);
            Assert.Equal(result.MessageType, messageType);
            Assert.Equal(result.Payload, sentData);
            Assert.True(extendedUdpClient.TrackedMessages.Count() == previousTrackMessagesSize + 1);
        }
    }
}
