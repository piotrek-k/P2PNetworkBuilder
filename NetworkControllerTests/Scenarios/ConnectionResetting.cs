using ConnectionsManager.Debugging;
using Microsoft.Extensions.Logging;
using Moq;
using NetworkController.UDP;
using NetworkControllerTests.Helper;
using NetworkControllerTests.Mocks;
using System;
using Xunit;
using Xunit.Abstractions;

namespace NetworkControllerTests.Scenarios
{
    public class ConnectionResetting
    {
        private Mock<ILogger<ConnectionResetting>> loggerMock;
        private ILogger logger;
        NetworkBehaviourTracker nbt;

        public ConnectionResetting(ITestOutputHelper output)
        {
            loggerMock = new Mock<ILogger<ConnectionResetting>>();
            logger = new LogToOutput(output, "logger").CreateLogger("category");
            nbt = new NetworkBehaviourTracker();
        }

        [Fact]
        public void ShouldSucceedIfNodeAgrees()
        {
            /**
             * Arrange
             */

            // Fake network
            FakeNetworkBuilder fnb = new FakeNetworkBuilder(logger, nbt);
            var device1Mock = fnb.GenerateFullMockOfNetworkController(Guid.NewGuid());
            device1Mock.Setup(x => x.ConnectionResetRule).Returns((guid) => { return true; });
            var device1 = device1Mock.Object;
            var device2Mock = fnb.GenerateFullMockOfNetworkController(Guid.NewGuid());
            device2Mock.Setup(x => x.ConnectionResetRule).Returns((guid) => { return true; });
            var device2 = device2Mock.Object;

            ExternalNode en1 = device2.AddNode(device1.DeviceId);
            ExternalNode en2 = device1.AddNode(device2.DeviceId);

            /**
             * Act
             */

            en2.RestartConnection();

            fnb.ProcessMessages();

            /**
            * Assert
            */

            Assert.Equal(ExternalNode.ConnectionState.Ready, en1.CurrentState);
            Assert.Equal(ExternalNode.ConnectionState.Ready, en2.CurrentState);
            Assert.Equal(en1.Ses.Aes.Key, en2.Ses.Aes.Key);
        }

        [Fact]
        public void ShouldFailIfNodeDisagrees()
        {
            /**
             * Arrange
             */

            // Fake network
            FakeNetworkBuilder fnb = new FakeNetworkBuilder(logger, nbt);
            var device1Mock = fnb.GenerateFullMockOfNetworkController(Guid.NewGuid());
            device1Mock.Setup(x => x.ConnectionResetRule).Returns((guid) => { return false; });
            var device1 = device1Mock.Object;
            var device2Mock = fnb.GenerateFullMockOfNetworkController(Guid.NewGuid());
            device2Mock.Setup(x => x.ConnectionResetRule).Returns((guid) => { return false; });
            var device2 = device2Mock.Object;

            ExternalNode en1 = device2.AddNode(device1.DeviceId);
            ExternalNode en2 = device1.AddNode(device2.DeviceId);

            /**
             * Act
             */

            en2.RestartConnection();

            fnb.ProcessMessages();

            /**
            * Assert
            */

            Assert.Equal(ExternalNode.ConnectionState.NotEstablished, en1.CurrentState);
            Assert.Equal(ExternalNode.ConnectionState.NotEstablished, en2.CurrentState);
        }
    }
}
