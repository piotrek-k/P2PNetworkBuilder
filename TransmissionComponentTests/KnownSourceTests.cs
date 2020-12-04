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
    public class KnownSourceTests
    {
        private ILogger _logger;

        public KnownSourceTests(ITestOutputHelper output)
        {
            _logger = new LogToOutput(output, "logger").CreateLogger("category");
        }

        [Theory]
        [InlineData(1, true)]
        [InlineData(-1, true)]
        [InlineData(1, false)]
        [InlineData(-1, false)]
        public void CounterResetting_ShouldChangeDirectrionOfCounting(int idModifier, bool sequentialModifier)
        {
            // Arrange
            Mock<ExtendedUdpClient> udpClientMock = new Mock<ExtendedUdpClient>(_logger);
            KnownSource knownSource = new KnownSource(udpClientMock.Object);
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 13000);

            List<int> sequenceTracker = new List<int>();
            udpClientMock.Setup(x => x.OnNewMessageReceived(It.IsAny<NewMessageEventArgs>())).Callback<NewMessageEventArgs>((evArg) =>
            {
                sequenceTracker.Add(evArg.DataFrame.RetransmissionId);
            });

            if (idModifier < 0)
                knownSource.ResetCounter(-1);

            // Act

            // Typical message exchange
            knownSource.HandleNewMessage(endpoint, new DataFrame()
            {
                RetransmissionId = 1 * idModifier,
                SendSequentially = sequentialModifier ? true : false
            });
            knownSource.HandleNewMessage(endpoint, new DataFrame()
            {
                RetransmissionId = 2 * idModifier,
                SendSequentially = sequentialModifier ? false : true
            });

            // Counter reset
            knownSource.ResetCounter(-4 * idModifier);

            // These messages should be rejected
            knownSource.HandleNewMessage(endpoint, new DataFrame()
            {
                RetransmissionId = 3 * idModifier,
                SendSequentially = sequentialModifier ? true : false
            });
            knownSource.HandleNewMessage(endpoint, new DataFrame()
            {
                RetransmissionId = 4 * idModifier,
                SendSequentially = sequentialModifier ? false : true
            });

            // These should be processed
            knownSource.HandleNewMessage(endpoint, new DataFrame()
            {
                RetransmissionId = -4 * idModifier,
                SendSequentially = sequentialModifier ? true : false
            });
            knownSource.HandleNewMessage(endpoint, new DataFrame()
            {
                RetransmissionId = -5 * idModifier,
                SendSequentially = sequentialModifier ? false : true
            });

            // Assert
            Assert.Equal(new List<int> { 1 * idModifier, 2 * idModifier, -4 * idModifier, -5 * idModifier }, sequenceTracker);
        }

        [Theory]
        [InlineData(int.MaxValue, 1, true)]
        [InlineData(int.MinValue, -1, true)]
        [InlineData(int.MaxValue, 1, false)]
        [InlineData(int.MinValue, -1, false)]
        public void CounterShould_PreventOverflow(int valueCausingOverflow, int idModifier, bool sequentialModifier)
        {
            // Arrange
            Mock<ExtendedUdpClient> udpClientMock = new Mock<ExtendedUdpClient>(_logger);
            KnownSource knownSource = new KnownSource(udpClientMock.Object);
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 13000);

            List<int> sequenceTracker = new List<int>();
            udpClientMock.Setup(x => x.OnNewMessageReceived(It.IsAny<NewMessageEventArgs>())).Callback<NewMessageEventArgs>((evArg) =>
            {
                sequenceTracker.Add(evArg.DataFrame.RetransmissionId);
            });

            knownSource.ResetCounter(valueCausingOverflow);

            // Act
            knownSource.HandleNewMessage(endpoint, new DataFrame()
            {
                RetransmissionId = valueCausingOverflow,
                SendSequentially = sequentialModifier ? true : false
            });

            knownSource.HandleNewMessage(endpoint, new DataFrame()
            {
                RetransmissionId = 1 * idModifier,
                SendSequentially = sequentialModifier ? true : false
            });
            knownSource.HandleNewMessage(endpoint, new DataFrame()
            {
                RetransmissionId = 2 * idModifier,
                SendSequentially = sequentialModifier ? true : false
            });

            // Assert
            Assert.Equal(new List<int> { valueCausingOverflow, 1 * idModifier, 2 * idModifier }, sequenceTracker);
        }
    }
}
