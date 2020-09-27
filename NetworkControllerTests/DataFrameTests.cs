using NetworkController;
using NetworkController.DataTransferStructures.Packers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            df.SourceNodeIdGuid = new Guid();
            df.Payload = new byte[] { 1, 2, 3 };
            df.ExpectAcknowledge = true;
            df.MessageType = 15;
            df.IV = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16};
            df.RetransmissionId = 50;

            // Act
            byte[] bytesToTransmit = df.PackToBytes();
            DataFrame receivedFrame = DataFrame.Unpack(bytesToTransmit);

            // Assert
            Assert.Equal(df.SourceNodeIdGuid, receivedFrame.SourceNodeIdGuid);
            Assert.Equal(df.Payload, receivedFrame.Payload);
            Assert.Equal(df.MessageType, receivedFrame.MessageType);
            Assert.Equal(df.ExpectAcknowledge, receivedFrame.ExpectAcknowledge);
            Assert.Equal(df.IV, receivedFrame.IV);
            Assert.Equal(df.RetransmissionId, receivedFrame.RetransmissionId);
        }

        [Fact]
        public void Should_Remain_Same_After_Packing_And_Unpacking_With_Null_Values()
        {
            // Arrange
            DataFrame df = new DataFrame();
            df.SourceNodeIdGuid = new Guid();
            df.Payload = null;
            df.ExpectAcknowledge = true;
            df.MessageType = 15;
            df.IV = null;
            df.RetransmissionId = 50;

            // Act
            byte[] bytesToTransmit = df.PackToBytes();
            DataFrame receivedFrame = DataFrame.Unpack(bytesToTransmit);

            // Assert
            Assert.Equal(df.SourceNodeIdGuid, receivedFrame.SourceNodeIdGuid);
            Assert.Equal(df.Payload, receivedFrame.Payload);
            Assert.Equal(df.MessageType, receivedFrame.MessageType);
            Assert.Equal(df.ExpectAcknowledge, receivedFrame.ExpectAcknowledge);
            Assert.Equal(df.IV, receivedFrame.IV);
            Assert.Equal(df.RetransmissionId, receivedFrame.RetransmissionId);
        }

        [Fact]
        public void Should_Have_Proper_Attributes()
        {
            List<PropertyInfo> properties = typeof(DataFrame).GetProperties(BindingFlags.Public | BindingFlags.Instance).ToList();

            int valuesToEncode = properties.Count(x => x.GetCustomAttribute<ValueToPackAttribute>() != null);
            int fixedSizedValues = properties.Count(x => x.GetCustomAttribute<FixedSizeAttribute>() != null);

            Assert.True(valuesToEncode <= fixedSizedValues + 1);

            var ordered = properties.Select((p) => new
            {
                prop = p,
                valToPack = p.GetCustomAttribute<ValueToPackAttribute>(),
                fixedSize = p.GetCustomAttribute<FixedSizeAttribute>()
            }).Where(x => x.valToPack != null).OrderBy(x => x.valToPack.PlaceInSequence);

            var lastPlace = ordered.Last().valToPack.PlaceInSequence;

            foreach (var o in ordered)
            {
                if (o.fixedSize == null && lastPlace != o.valToPack.PlaceInSequence)
                {
                    throw new Exception("One non-fixed size allowed at the end of sequence");
                }
            }
        }

        [Fact]
        public void Size_Estimator_Should_Return_Overall_Size_Minus_Payload()
        {
            // Arrange
            DataFrame df = new DataFrame();
            df.SourceNodeIdGuid = new Guid();
            df.Payload = new byte[] { 1, 2, 3 };
            df.ExpectAcknowledge = true;
            df.MessageType = 15;
            df.IV = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
            df.RetransmissionId = 50;

            // Act
            int estimatedSize = DataFrame.EstimateSize();
            int realDataFrameSize = df.PackToBytes().Length;

            // Assert
            Assert.True(estimatedSize == realDataFrameSize - df.Payload.Length);
        }
    }
}
