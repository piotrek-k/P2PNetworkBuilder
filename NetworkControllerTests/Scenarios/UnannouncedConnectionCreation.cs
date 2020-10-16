using Castle.DynamicProxy.Generators.Emitters.SimpleAST;
using ConnectionsManager.Debugging;
using ConnectionsManager.Tests;
using Microsoft.Extensions.Logging;
using Moq;
using NetworkController.UDP;
using NetworkControllerTests.Helper;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace NetworkControllerTests.Scenarios
{
    public class UnannouncedConnectionCreation
    {
        private Mock<ILogger<UnannouncedConnectionCreation>> loggerMock;
        private ILogger logger;
        NetworkBehaviourTracker nbt;

        public UnannouncedConnectionCreation(ITestOutputHelper output)
        {
            loggerMock = new Mock<ILogger<UnannouncedConnectionCreation>>();
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
            device1Mock.Setup(x => x.NewUnannouncedConnectionAllowanceRule).Returns((guid) => { return true; });
            var device1 = device1Mock.Object;
            var device2 = fnb.GenerateFullMockOfNetworkController(Guid.NewGuid()).Object;

            ExternalNode en1 = device2.AddNode(device1.DeviceId);
            ExternalNode en2 = device1.AddNode(device2.DeviceId);

            /**
             * Act
             */

            en2.InitializeConnection();

            fnb.ProcessMessages();

            /**
            * Assert
            */

            Assert.Equal(ExternalNode.ConnectionState.Ready, en1.CurrentState);
            Assert.Equal(ExternalNode.ConnectionState.Ready, en2.CurrentState);
            Assert.Equal(en1.Ses.Aes.Key, en2.Ses.Aes.Key);
        }
    }
}
