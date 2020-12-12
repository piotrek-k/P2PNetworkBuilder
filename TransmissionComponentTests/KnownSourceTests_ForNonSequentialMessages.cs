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
        public void HandleNewMessageShould_ProcessNonSequentialMessagesOutOfOrder_WithoutDuplicates()
        {
            // Arrange
            Mock<IUdpClient> internetTransmissionMock = new Mock<IUdpClient>();
            Mock<ExtendedUdpClient> udpClientMock = new Mock<ExtendedUdpClient>(internetTransmissionMock.Object, _logger);
            KnownSource knownSource = new KnownSource(udpClientMock.Object, Guid.NewGuid(), _logger);
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 13000);
            Guid sourceId = Guid.NewGuid();

            knownSource.NextExpectedIncomingMessageId = 1;
            int testedMessageId = 10;

            DataFrame df = new DataFrame()
            {
                SourceNodeIdGuid = sourceId,
                RetransmissionId = testedMessageId,
                SendSequentially = false
            };

            // Act
            knownSource.HandleNewMessage(endpoint, df);
            knownSource.HandleNewMessage(endpoint, df);
            knownSource.HandleNewMessage(endpoint, df);

            // Assert
            udpClientMock.Verify(x => x.OnNewMessageReceived(It.IsAny<NewMessageEventArgs>()), Times.Once);
            Assert.True(knownSource.ProcessedMessages.Count() == 1);
        }

        [Fact]
        public void UnneededValuesOfProcessedMessagesShouldBeRemoved()
        {
            // Arrange
            Mock<IUdpClient> internetTransmissionMock = new Mock<IUdpClient>();
            Mock<ExtendedUdpClient> udpClientMock = new Mock<ExtendedUdpClient>(internetTransmissionMock.Object, _logger);
            KnownSource knownSource = new KnownSource(udpClientMock.Object, Guid.NewGuid(), _logger);
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 13000);

            // Act
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
                RetransmissionId = 4,
                SendSequentially = false
            });

            // Assert
            Assert.True(knownSource.ProcessedMessages.Count() == 3);

            knownSource.HandleNewMessage(endpoint, new DataFrame()
            {
                RetransmissionId = 1,
                SendSequentially = false
            });

            // Assert
            Assert.Empty(knownSource.ProcessedMessages);
        }
    }
}
