using ConnectionsManager.Debugging;
using Microsoft.Extensions.Logging;
using Moq;
using NetworkController.DataTransferStructures;
using NetworkController.DataTransferStructures.Other;
using NetworkController.Encryption;
using NetworkController.Models;
using NetworkController.UDP;
using NetworkControllerTests.Helper;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace NetworkControllerTests.IncomingMessages
{
    public class ConnectionRestoring
    {
        private Mock<ILogger<HandshakingControllerTest>> logger;
        NetworkBehaviourTracker nbt;

        public ConnectionRestoring()
        {
            logger = new Mock<ILogger<HandshakingControllerTest>>();
            nbt = new NetworkBehaviourTracker();
        }

        [Fact]
        public void ShouldFailIfEncryptionKeyIsDifferent()
        {
            /**
             * Arrange
             */

            // Fake network
            FakeNetworkBuilder fnb = new FakeNetworkBuilder(logger.Object, nbt);
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
            FakeNetworkBuilder fnb = new FakeNetworkBuilder(logger.Object, nbt);
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
            FakeNetworkBuilder fnb = new FakeNetworkBuilder(logger.Object, nbt);
            var device1 = fnb.GenerateFullMockOfNetworkController(Guid.NewGuid());
            var device2 = fnb.GenerateFullMockOfNetworkController(Guid.NewGuid());

            var validKeys = new SymmetricEncryptionService();

            // Each side have different keys
            ExternalNode en1 = device2.AddNode(device1.DeviceId);
            en1.Ses = validKeys;
            ExternalNode en2 = device1.AddNode(device2.DeviceId);
            en2.Ses = new SymmetricEncryptionService();

            /**
             * Act
             */

            // Restore keys
            en2.RestoreSecurityKeys(validKeys.Aes.Key);

            /**
             * Assert
             */
            Assert.Equal(ExternalNode.ConnectionState.Ready, en1.CurrentState);
            Assert.Equal(ExternalNode.ConnectionState.Ready, en2.CurrentState);
            Assert.Equal(en1.HighestReceivedSendingId, en2.HighestReceivedSendingId);
        }
    }
}
