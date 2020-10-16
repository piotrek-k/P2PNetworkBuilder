using ConnectionsManager.Debugging;
using ConnectionsManager.Tests;
using Microsoft.Extensions.Logging;
using Moq;
using NetworkController.DataTransferStructures;
using NetworkController.DataTransferStructures.Other;
using NetworkController.Encryption;
using NetworkController.Models;
using NetworkController.UDP;
using NetworkControllerTests.Helper;
using System;
using Xunit;
using Xunit.Abstractions;

namespace NetworkControllerTests.IncomingMessages
{
    public class ConnectionRestoring
    {
        private Mock<ILogger<HandshakingControllerTest>> loggerMock;
        private ILogger logger;
        NetworkBehaviourTracker nbt;

        public ConnectionRestoring(ITestOutputHelper output)
        {
            loggerMock = new Mock<ILogger<HandshakingControllerTest>>();
            logger = new LogToOutput(output, "logger").CreateLogger("category");
            nbt = new NetworkBehaviourTracker();
        }

        [Fact]
        public void ShouldFailIfEncryptionKeyIsDifferent()
        {
            /**
             * Arrange
             */

            // Fake network
            FakeNetworkBuilder fnb = new FakeNetworkBuilder(loggerMock.Object, nbt);
            var device1 = fnb.GenerateFullMockOfNetworkController(Guid.NewGuid());
            var device2 = fnb.GenerateFullMockOfNetworkController(Guid.NewGuid());

            // Each side have different keys
            ExternalNode en1 = device2.AddNode(device1.DeviceId);
            en1.Ses = new SymmetricEncryptionService();
            ExternalNode en2 = device1.AddNode(device2.DeviceId);
            en2.Ses = new SymmetricEncryptionService();

            AckStatus? resultStatus = null;

            /**
             * Act
             */

            // Send ConnectionRestoreRequest from device1 to device2
            en2.SendBytes((int)MessageType.ConnectionRestoreRequest, new ConnectionRestoreRequest
            {
                SampleDataForEncryptionVerification = ExternalNode.SAMPLE_ENCRYPTION_VERIFICATION_TEXT
            }.PackToBytes(), (s) =>
            {
                resultStatus = s;
            });

            /**
              * Assert
              */

            Assert.True(resultStatus == AckStatus.Failure);
        }

        [Fact]
        public void ShouldPassIfEncryptionKeysAreTheSame()
        {
            /**
             * Arrange
             */

            // Fake network
            FakeNetworkBuilder fnb = new FakeNetworkBuilder(loggerMock.Object, nbt);
            var device1 = fnb.GenerateFullMockOfNetworkController(Guid.NewGuid());
            var device2 = fnb.GenerateFullMockOfNetworkController(Guid.NewGuid());

            // Keys are the same
            ExternalNode en1 = device2.AddNode(device1.DeviceId);
            en1.Ses = new SymmetricEncryptionService();
            ExternalNode en2 = device1.AddNode(device2.DeviceId);
            en2.Ses = en1.Ses;

            AckStatus? resultStatus = null;

            /**
             * Act
             */

            // Send ConnectionRestoreRequest from device1 to device2
            en2.SendBytes((int)MessageType.ConnectionRestoreRequest, new ConnectionRestoreRequest
            {
                SampleDataForEncryptionVerification = ExternalNode.SAMPLE_ENCRYPTION_VERIFICATION_TEXT
            }.PackToBytes(), (s) =>
            {
                resultStatus = s;
            });

            /**
              * Assert
              */

            Assert.True(resultStatus == AckStatus.Success);
        }

        [Fact]
        public void RestoreSecurityKeysShouldRestoreConnectionAndSynchronizeCounters()
        {
            /**
            * Arrange
            */

            // Fake network
            FakeNetworkBuilder fnb = new FakeNetworkBuilder(logger, nbt);
            var device1 = fnb.GenerateFullMockOfNetworkController(Guid.NewGuid());
            var device2 = fnb.GenerateFullMockOfNetworkController(Guid.NewGuid());

            var validKeys = new SymmetricEncryptionService();

            // Each side have different keys
            ExternalNode en1 = device2.AddNode(device1.DeviceId);
            en1.Ses = validKeys;
            ExternalNode en2 = device1.AddNode(device2.DeviceId);
            en2.Ses = new SymmetricEncryptionService();

            bool response1Received = false;
            bool response2Received = false;

            /**
             * Act
             */

            en1.SendBytes(9999, null);
            en1.SendBytes(9999, null);
            en1.SendBytes(9999, null);
            en1.SendBytes(9999, null);
            en1.SendBytes(9999, null);

            // Restore keys
            en2.RestoreSecurityKeys(validKeys.Aes.Key);

            

            // Checking response to random messages
            // Incorrectly set HighestReceivedSendingId would cause to omit those messages
            en2.SendBytes(9999, null, (s) =>
            {
                response1Received = true;
            });

            en1.SendBytes(9999, null, (s) =>
            {
                response2Received = true;
            });

            /**
             * Assert
             */
            Assert.Equal(ExternalNode.ConnectionState.Ready, en1.CurrentState);
            Assert.Equal(ExternalNode.ConnectionState.Ready, en2.CurrentState);
            //Assert.Equal(en1.HighestReceivedSendingId, en2.HighestReceivedSendingId);
            Assert.True(response1Received);
            Assert.True(response2Received);
        }
    }
}
