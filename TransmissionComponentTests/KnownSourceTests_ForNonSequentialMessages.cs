using Microsoft.Extensions.Logging;
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
    public class KnownSourceTests_ForNonSequentialMessages
    {
        private ILogger _logger;

        public KnownSourceTests_ForNonSequentialMessages(ITestOutputHelper output)
        {
            _logger = new LogToOutput(output, "logger").CreateLogger("category");
        }

        [Fact]
        public void HandleNewMessageShould_ProcessNonSequentialMessagesOutOfOrder()
        {
            // Arrange
            Mock<ExtendedUdpClient> udpClientMock = new Mock<ExtendedUdpClient>(_logger);
            KnownSource knownSource = new KnownSource(udpClientMock.Object);
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 13000);
            Guid sourceId = Guid.NewGuid();

            knownSource.NextExpectedIncomingMessageId = 1;
            uint testedMessageId = 10;

            DataFrame df = new DataFrame()
            {
                SourceNodeIdGuid = sourceId,
                RetransmissionId = testedMessageId,
                SendSequentially = false
            };

            // Act
            knownSource.HandleNewMessage(endpoint, df);

            // Assert
            udpClientMock.Verify(x => x.OnNewMessageReceived(It.IsAny<NewMessageEventArgs>()), Times.Once);
            Assert.True(knownSource.ProcessedMessages.Count() == 1);
        }
    }
}
