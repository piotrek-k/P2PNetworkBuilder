using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Linq;
using System.Net;
using TransmissionComponent;
using TransmissionComponent.Others;
using TransmissionComponent.Structures;
using TransmissionComponent.Structures.Other;
using Xunit;
using Xunit.Abstractions;

namespace TransmissionComponentTests
{
    public class ExtendedUdpClientTests
    {
        private ILogger _logger;

        public ExtendedUdpClientTests(ITestOutputHelper output)
        {
            _logger = new LogToOutput(output, "logger").CreateLogger("category");
        }

        [Fact]
        public void SendMessageSequentiallyShould_PackMessage_PassItToUdpClient_StartTracking()
        {
            // Arrange
            Mock<IUdpClient> udpClientMock = new Mock<IUdpClient>();
            ExtendedUdpClient extendedUdpClient = new ExtendedUdpClient(udpClientMock.Object, _logger);
            Action<AckStatus> callback = (status) => { };

            byte[] receivedData = null;
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 13000);
            int messageType = 1;
            byte[] sentData = new byte[] { 1, 2, 3, 4 };
            int previousTrackMessagesSize = extendedUdpClient.TrackedMessages.Count();
            Guid sourceGuid = Guid.NewGuid();
            byte[] encryptionSeed = Enumerable.Repeat((byte)1, 16).ToArray();
            int currentMessageId = extendedUdpClient.NextSentMessageId;

            udpClientMock.Setup(x => x.Send(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<IPEndPoint>()))
                .Callback<byte[], int, IPEndPoint>((dgram, bytes, endpoint) =>
                {
                    receivedData = dgram;
                });

            // Act 
            extendedUdpClient.SendMessageSequentially(
                endpoint, messageType, sentData, sourceGuid, encryptionSeed, callback);

            // Assert
            udpClientMock
                .Verify(x => x.Send(It.IsAny<byte[]>(), It.IsAny<int>(), It.Is<IPEndPoint>(x => x == endpoint)),
                    Times.Once);

            DataFrame result = DataFrame.Unpack(receivedData);
            Assert.True(result.ExpectAcknowledge);
            Assert.Equal(result.MessageType, messageType);
            Assert.Equal(result.Payload, sentData);
            Assert.True(extendedUdpClient.TrackedMessages.Count() == previousTrackMessagesSize + 1);
            Assert.True(extendedUdpClient.NextSentMessageId == currentMessageId + 1);
        }

        [Fact]
        public void RetransmissionThread_Should_SendMessageWhenItsPresentInTrackedMessages()
        {
            // Arrange
            Mock<IUdpClient> udpClientMock = new Mock<IUdpClient>();
            ExtendedUdpClient extendedUdpClient = new ExtendedUdpClient(udpClientMock.Object, _logger);

            IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 13000);
            extendedUdpClient.WaitingTimeBetweenRetransmissions = 0; // waiting time to zero
            TrackedMessage tm = new TrackedMessage(new byte[] { 1, 2, 3, 4 }, endpoint);
            int messageId = 1;
            extendedUdpClient.TrackedMessages.Add(messageId, tm);

            // Act and Assert
            int initalLoop = 3;
            for (int x = 1; x <= initalLoop; x++)
            {
                extendedUdpClient.RetransmissionThread(messageId, tm);
                udpClientMock
                    .Verify(x => x.Send(It.IsAny<byte[]>(), It.IsAny<int>(), It.Is<IPEndPoint>(x => x == endpoint)),
                        Times.Exactly(x));
            }

            // remove from list
            extendedUdpClient.TrackedMessages.Remove(messageId);
            // call thread again
            extendedUdpClient.RetransmissionThread(messageId, tm);

            // Number of UdpClient calls should not change
            udpClientMock
                   .Verify(x => x.Send(It.IsAny<byte[]>(), It.IsAny<int>(), It.Is<IPEndPoint>(x => x == endpoint)),
                       Times.Exactly(initalLoop));
        }

        [Fact]
        public void HandleIncomingMessagesShould_CreateNewSourceWhenUnknown()
        {
            // Arrange
            Mock<IUdpClient> udpClientMock = new Mock<IUdpClient>();
            ExtendedUdpClient extendedUdpClient = new ExtendedUdpClient(udpClientMock.Object, _logger);

            extendedUdpClient.NewIncomingMessage = (x) => { return AckStatus.Success; };

            int testedMessageId = 1;
            Guid sourceId = Guid.NewGuid();

            udpClientMock.Setup(x => x.EndReceive(It.IsAny<IAsyncResult>(), ref It.Ref<IPEndPoint>.IsAny))
                .Returns(() =>
                {
                    return new DataFrame()
                    {
                        SourceNodeIdGuid = sourceId,
                        RetransmissionId = testedMessageId
                    }.PackToBytes();
                });

            int previousKnownSourceCount = extendedUdpClient.KnownSources.Count();

            // Act
            extendedUdpClient.HandleIncomingMessages(null);
            extendedUdpClient.HandleIncomingMessages(null);

            // called twice, should be added once

            // Assert
            Assert.True(extendedUdpClient.KnownSources.Count() == previousKnownSourceCount + 1);
        }
    }
}
