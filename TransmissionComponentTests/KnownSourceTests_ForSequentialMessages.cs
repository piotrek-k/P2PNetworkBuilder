﻿using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using TransmissionComponent;
using TransmissionComponent.Others;
using TransmissionComponent.Structures;
using Xunit;
using Xunit.Abstractions;

namespace TransmissionComponentTests
{
    public class KnownSourceTests_ForSequentialMessages
    {
        private ILogger _logger;

        public KnownSourceTests_ForSequentialMessages(ITestOutputHelper output)
        {
            _logger = new LogToOutput(output, "logger").CreateLogger("category");
        }

        [Fact]
        public void HandleNewMessageShould_ProcessImmadiatelyMessageWithProperId()
        {
            // Arrange
            Mock<ExtendedUdpClient> udpClientMock = new Mock<ExtendedUdpClient>(_logger);
            KnownSource knownSource = new KnownSource(udpClientMock.Object);
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 13000);
            Guid sourceId = Guid.NewGuid();

            uint testedMessageId = 1;
            knownSource.NextExpectedIncomingMessageId = 1;

            DataFrame df = new DataFrame()
            {
                SourceNodeIdGuid = sourceId,
                RetransmissionId = testedMessageId,
                SendSequentially = true
            };

            // Act
            knownSource.HandleNewMessage(endpoint, df);

            // Assert
            udpClientMock.Verify(x => x.OnNewMessageReceived(It.IsAny<NewMessageEventArgs>()));
        }

        [Theory]
        [InlineData(2)]
        [InlineData(int.MaxValue)]
        public void HandleNewMessageShould_PostponeProcessingMessageWithImproperId(uint testedMessageId)
        {
            // Arrange
            Mock<ExtendedUdpClient> udpClientMock = new Mock<ExtendedUdpClient>(_logger);
            KnownSource knownSource = new KnownSource(udpClientMock.Object);
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 13000);
            Guid sourceId = Guid.NewGuid();

            knownSource.NextExpectedIncomingMessageId = 1;

            DataFrame df = new DataFrame()
            {
                SourceNodeIdGuid = sourceId,
                RetransmissionId = testedMessageId,
                SendSequentially = true
            };

            // Act
            knownSource.HandleNewMessage(endpoint, df);

            // Assert
            udpClientMock.Verify(x => x.OnNewMessageReceived(It.IsAny<NewMessageEventArgs>()), Times.Never);
            Assert.True(knownSource.WaitingMessages.Count() == 1);
        }

        [Fact]
        public void HandleNewMessageShould_ImmadiatelyProcessWaitingMessagesIfItsTheirTurn()
        {
            // Arrange
            Mock<ExtendedUdpClient> udpClientMock = new Mock<ExtendedUdpClient>(_logger);
            KnownSource knownSource = new KnownSource(udpClientMock.Object);
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 13000);

            DataFrame df_that_came_too_fast_1 = new DataFrame()
            {
                RetransmissionId = 3,
                SendSequentially = true
            };

            DataFrame df_that_came_too_fast_2 = new DataFrame()
            {
                RetransmissionId = 2,
                SendSequentially = true
            };

            DataFrame df_to_process_first = new DataFrame()
            {
                RetransmissionId = 1,
                SendSequentially = true
            };

            // Act 

            knownSource.HandleNewMessage(endpoint, df_that_came_too_fast_1);
            knownSource.HandleNewMessage(endpoint, df_that_came_too_fast_2);

            // At this point nothing should be processed
            udpClientMock.Verify(x => x.OnNewMessageReceived(It.IsAny<NewMessageEventArgs>()), Times.Never);

            knownSource.HandleNewMessage(endpoint, df_to_process_first);

            // Assert
            udpClientMock.Verify(x => x.OnNewMessageReceived(It.Is<NewMessageEventArgs>(x => x.DataFrame.RetransmissionId == 1)), Times.Once);
            udpClientMock.Verify(x => x.OnNewMessageReceived(It.Is<NewMessageEventArgs>(x => x.DataFrame.RetransmissionId == 2)), Times.Once);
            udpClientMock.Verify(x => x.OnNewMessageReceived(It.Is<NewMessageEventArgs>(x => x.DataFrame.RetransmissionId == 3)), Times.Once);
        }

        [Fact]
        public void HandleNewMessagesShould_PreventProcessingSameMessageMoreThanOnce()
        {
            // Arrange
            Mock<ExtendedUdpClient> udpClientMock = new Mock<ExtendedUdpClient>(_logger);
            KnownSource knownSource = new KnownSource(udpClientMock.Object);
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 13000);

            DataFrame message = new DataFrame()
            {
                RetransmissionId = 1,
                SendSequentially = true
            };

            // Act
            knownSource.HandleNewMessage(endpoint, message);
            knownSource.HandleNewMessage(endpoint, message);
            knownSource.HandleNewMessage(endpoint, message);

            // Assert
            udpClientMock.Verify(x => x.OnNewMessageReceived(It.IsAny<NewMessageEventArgs>()), Times.Once);
        }

        [Fact]
        public void HandleNewMessagesShould_HandleMixedSequentialAndNonsequentialMessages()
        {
            // Arrange
            Mock<ExtendedUdpClient> udpClientMock = new Mock<ExtendedUdpClient>(_logger);
            KnownSource knownSource = new KnownSource(udpClientMock.Object);
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 13000);

            List<uint> sequenceTracker = new List<uint>();
            udpClientMock.Setup(x => x.OnNewMessageReceived(It.IsAny<NewMessageEventArgs>())).Callback<NewMessageEventArgs>((evArg) =>
            {
                sequenceTracker.Add(evArg.DataFrame.RetransmissionId);
            });

            // Act
            knownSource.HandleNewMessage(endpoint, new DataFrame()
            {
                RetransmissionId = 4,
                SendSequentially = true
            });
            knownSource.HandleNewMessage(endpoint, new DataFrame()
            {
                RetransmissionId = 2,
                SendSequentially = false
            });
            knownSource.HandleNewMessage(endpoint, new DataFrame()
            {
                RetransmissionId = 3,
                SendSequentially = false
            });
            knownSource.HandleNewMessage(endpoint, new DataFrame()
            {
                RetransmissionId = 1,
                SendSequentially = true
            });
            knownSource.HandleNewMessage(endpoint, new DataFrame()
            {
                RetransmissionId = 5,
                SendSequentially = true
            });

            // Assert
            udpClientMock.Verify(x => x.OnNewMessageReceived(It.IsAny<NewMessageEventArgs>()), Times.Exactly(5));
            Assert.Equal(new List<uint> { 2, 3, 1, 4, 5 }, sequenceTracker);
        }
    }
}
