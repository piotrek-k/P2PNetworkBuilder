using NetworkController.DataTransferStructures;
using NetworkController.DataTransferStructures.Other;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace NetworkController.Interfaces
{
    public interface ITransmissionManager
    {
        void SendFrameEnsureDelivered(DataFrame df, IPEndPoint destination, Action<AckStatus> callback = null);
        void SendFrameAndForget(DataFrame df, IPEndPoint destination);

        void ReportReceivingDataArrivalAcknowledge(DataFrame df, ReceiveAcknowledge receivedPayload);

        void SetupIfNotWorking(uint startingValue, IExternalNode node = null);
        void GentleShutdown();
        void Destroy();
    }
}
