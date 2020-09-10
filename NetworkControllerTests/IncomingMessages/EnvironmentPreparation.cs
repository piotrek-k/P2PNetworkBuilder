using Moq;
using NetworkController;
using NetworkController.DataTransferStructures.Other;
using NetworkController.Interfaces;
using NetworkController.Interfaces.ForTesting;
using NetworkController.Models;
using NetworkController.UDP;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace NetworkControllerTests.IncomingMessages
{
    public class EnvironmentPreparation
    {
        public static void PerformFullHandshaking(
            Mock<ITransmissionManager> transmissionManagerMockOne,
            Mock<ITransmissionManager> transmissionManagerMockTwo,
            ExternalNode nodeOne, ExternalNode nodeTwo)
        {
            transmissionManagerMockOne.Setup(x => x.SendFrameEnsureDelivered(It.IsAny<DataFrame>(), It.IsAny<IPEndPoint>(), It.IsAny<Action<AckStatus>>()))
              .Callback<DataFrame, IPEndPoint, Action>((df, ep, c) => nodeTwo.HandleIncomingBytes(df));
            transmissionManagerMockOne.Setup(x => x.SendFrameAndForget(It.IsAny<DataFrame>(), It.IsAny<IPEndPoint>()))
              .Callback<DataFrame, IPEndPoint>((df, ep) => nodeTwo.HandleIncomingBytes(df));

            transmissionManagerMockTwo.Setup(x => x.SendFrameEnsureDelivered(It.IsAny<DataFrame>(), It.IsAny<IPEndPoint>(), It.IsAny<Action<AckStatus>>()))
              .Callback<DataFrame, IPEndPoint, Action>((df, ep, c) => nodeOne.HandleIncomingBytes(df));
            transmissionManagerMockTwo.Setup(x => x.SendFrameAndForget(It.IsAny<DataFrame>(), It.IsAny<IPEndPoint>()))
              .Callback<DataFrame, IPEndPoint>((df, ep) => nodeOne.HandleIncomingBytes(df));

            nodeOne.InitializeConnection();
        }
    }
}
