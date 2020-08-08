using NetworkController;
using System;
using Xunit;

namespace NetworkControllerTests
{
    public class DataFrameTests
    {
        [Fact]
        public void Should_Remain_Same_After_Packing_And_Unpacking()
        {
            // Arrange
            DataFrame df = new DataFrame();
            df.SourceNodeId = new Guid();
            df.Payload = new byte[] { 1, 2, 3 };

            // Act
            byte[] bytesToTransmit = df.PackToBytes();
            DataFrame receivedFrame = DataFrame.Unpack(bytesToTransmit);

            // Assert
            Assert.Equal(df.SourceNodeId, receivedFrame.SourceNodeId);
            Assert.Equal(df.Payload, receivedFrame.Payload);
            Assert.Equal(df.MessageType, receivedFrame.MessageType);
        }
    }
}
