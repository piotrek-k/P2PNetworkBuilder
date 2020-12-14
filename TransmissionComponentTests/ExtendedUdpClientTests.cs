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
            Guid deviceGuid = Guid.NewGuid();
            ExtendedUdpClient extendedUdpClient = new ExtendedUdpClient(udpClientMock.Object, _logger, deviceGuid);
            Action<AckStatus> callback = (status) => { };

            Guid destinationGuid = Guid.NewGuid();
            byte[] receivedData = null;
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 13000);
            byte[] sentData = new byte[] { 1, 2, 3, 4 };
            var foundSource = extendedUdpClient.FindOrCreateSource(destinationGuid);
            int previousTrackMessagesSize = foundSource.TrackedOutgoingMessages.Count();
            KnownSource ks = extendedUdpClient.FindOrCreateSource(destinationGuid);
            int currentMessageId = ks.NextIdForMessageToSend;

            udpClientMock.Setup(x => x.Send(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<IPEndPoint>()))
                .Callback<byte[], int, IPEndPoint>((dgram, bytes, endpoint) =>
                {
                    receivedData = dgram;
                });

            // Act 
            extendedUdpClient.SendMessageSequentially(
                endpoint, sentData, deviceGuid, destinationGuid, callback);

            // Assert
            udpClientMock
                .Verify(x => x.Send(It.IsAny<byte[]>(), It.IsAny<int>(), It.Is<IPEndPoint>(x => x == endpoint)),
                    Times.Once);

            DataFrame result = DataFrame.Unpack(receivedData);
            Assert.True(result.ExpectAcknowledge);
            Assert.Equal(result.Payload, sentData);
            Assert.True(foundSource.TrackedOutgoingMessages.Count() == previousTrackMessagesSize + 1);
            Assert.True(ks.NextIdForMessageToSend == currentMessageId + 1);
        }

        //[Fact]
        //public void RetransmissionThread_Should_SendMessageWhenItsPresentInTrackedMessages()
        //{
        //    // Arrange
        //    Mock<IUdpClient> udpClientMock = new Mock<IUdpClient>();
        //    ExtendedUdpClient extendedUdpClient = new ExtendedUdpClient(udpClientMock.Object, _logger);

        //    IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 13000);
        //    extendedUdpClient.WaitingTimeBetweenRetransmissions = 0; // waiting time to zero
        //    TrackedMessage tm = new TrackedMessage(new byte[] { 1, 2, 3, 4 }, endpoint);
        //    int messageId = 1;
        //    extendedUdpClient.TrackedMessages.Add(messageId, tm);

        //    // Act and Assert
        //    int initalLoop = 3;
        //    for (int x = 1; x <= initalLoop; x++)
        //    {
        //        extendedUdpClient.RetransmissionThread(messageId, tm);
        //        udpClientMock
        //            .Verify(x => x.Send(It.IsAny<byte[]>(), It.IsAny<int>(), It.Is<IPEndPoint>(x => x == endpoint)),
        //                Times.Exactly(x));
        //    }

        //    // remove from list
        //    extendedUdpClient.TrackedMessages.Remove(messageId);
        //    // call thread again
        //    extendedUdpClient.RetransmissionThread(messageId, tm);

        //    // Number of UdpClient calls should not change
        //    udpClientMock
        //           .Verify(x => x.Send(It.IsAny<byte[]>(), It.IsAny<int>(), It.Is<IPEndPoint>(x => x == endpoint)),
        //               Times.Exactly(initalLoop));
        //}

        [Fact]
        public void HandleIncomingMessagesShould_CreateNewSourceWhenUnknown()
        {
            // Arrange
            Mock<IUdpClient> udpClientMock = new Mock<IUdpClient>();
            Guid deviceGuid = Guid.NewGuid();
            ExtendedUdpClient extendedUdpClient = new ExtendedUdpClient(udpClientMock.Object, _logger, deviceGuid);

            extendedUdpClient.NewIncomingMessage = (x) => { return AckStatus.Success; };

            int testedMessageId = 1;
            
            udpClientMock.Setup(x => x.EndReceive(It.IsAny<IAsyncResult>(), ref It.Ref<IPEndPoint>.IsAny))
                .Returns(() =>
                {
                    return new DataFrame()
                    {
                        SourceNodeIdGuid = deviceGuid,
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

        [Fact]
        public void HandleIncomingMessagesShould_StopRetransmittingAfterReceivingAck()
        {
            // Arrange
            Mock<IUdpClient> udpClientMock = new Mock<IUdpClient>();
            Guid deviceGuid = Guid.NewGuid();
            ExtendedUdpClient extendedUdpClient = new ExtendedUdpClient(udpClientMock.Object, _logger, deviceGuid);
            extendedUdpClient.NewIncomingMessage = (x) => { return AckStatus.Success; };

            Guid externalDeviceGuid = Guid.NewGuid();
            int testedMessageId = 1;
            udpClientMock.Setup(x => x.EndReceive(It.IsAny<IAsyncResult>(), ref It.Ref<IPEndPoint>.IsAny))
                .Returns(() =>
                {
                    return new DataFrame()
                    {
                        SourceNodeIdGuid = externalDeviceGuid,
                        RetransmissionId = testedMessageId,
                        ReceiveAck = true
                    }.PackToBytes();
                });

            
            var foundSource = extendedUdpClient.FindOrCreateSource(externalDeviceGuid);

            foundSource.TrackedOutgoingMessages.Add(
                testedMessageId, new TrackedMessage(
                    new byte[] { 1, 2 },
                    new IPEndPoint(IPAddress.Parse("127.0.0.1"), 13000)
                    ));
            foundSource.TrackedOutgoingMessages.Add(
               testedMessageId + 1, new TrackedMessage(
                   new byte[] { 1, 2 },
                   new IPEndPoint(IPAddress.Parse("127.0.0.1"), 13000)
                   ));

            Assert.Equal(2, foundSource.TrackedOutgoingMessages.Count);

            // Act
            extendedUdpClient.HandleIncomingMessages(null);

            // Assert
            Assert.Single(foundSource.TrackedOutgoingMessages);
        }

        [Theory]
        [InlineData(AckStatus.Success)]
        [InlineData(AckStatus.Failure)]
        public void HandleIncomingMessagesShould_RunCallBackWhenAcknowledgeArrives(AckStatus messageStatus)
        {
            // Arrange
            Mock<IUdpClient> udpClientMock = new Mock<IUdpClient>();
            Guid deviceGuid = Guid.NewGuid();
            ExtendedUdpClient extendedUdpClient = new ExtendedUdpClient(udpClientMock.Object, _logger, deviceGuid);
            extendedUdpClient.NewIncomingMessage = (x) => { return AckStatus.Success; };
            int testedMessageId = 1;
            int numberOfFailures = 0;
            int numberOfSuccesses = 0;
            Guid externalDeviceGuid = Guid.NewGuid();

            udpClientMock.Setup(x => x.EndReceive(It.IsAny<IAsyncResult>(), ref It.Ref<IPEndPoint>.IsAny))
                .Returns(() =>
                {
                    return new DataFrame()
                    {
                        SourceNodeIdGuid = externalDeviceGuid,
                        RetransmissionId = testedMessageId,
                        ReceiveAck = true,
                        Payload = new ReceiveAcknowledge() { Status = (int)messageStatus }.PackToBytes()
                    }.PackToBytes();
                });
            
            var foundSource = extendedUdpClient.FindOrCreateSource(externalDeviceGuid);

            foundSource.TrackedOutgoingMessages.Add(
                testedMessageId, new TrackedMessage(
                    new byte[] { 1, 2 },
                    new IPEndPoint(IPAddress.Parse("127.0.0.1"), 13000),
                    (status) =>
                    {
                        if (status == AckStatus.Failure)
                            numberOfFailures++;
                        else if (status == AckStatus.Success)
                            numberOfSuccesses++;
                    }
                    ));
            foundSource.TrackedOutgoingMessages.Add(
               testedMessageId + 1, new TrackedMessage(
                   new byte[] { 1, 2 },
                   new IPEndPoint(IPAddress.Parse("127.0.0.1"), 13000),
                   (status) =>
                   {
                       if (status == AckStatus.Failure)
                           numberOfFailures++;
                       else if (status == AckStatus.Success)
                           numberOfSuccesses++;
                   }
                   ));

            // Act
            extendedUdpClient.HandleIncomingMessages(null);
            extendedUdpClient.HandleIncomingMessages(null);
            extendedUdpClient.HandleIncomingMessages(null);

            // Assert
            Assert.Equal(1, messageStatus == AckStatus.Success ? numberOfSuccesses : numberOfFailures);
        }
    }
}
