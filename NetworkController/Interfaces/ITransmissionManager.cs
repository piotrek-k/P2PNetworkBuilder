using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace NetworkController.Interfaces
{
    public interface ITransmissionManager
    {
        void SendFrameEnsureDelivered(DataFrame df, IPEndPoint destination, Action callback = null);
        void SendFrameAndForget(DataFrame df, IPEndPoint destination);

        void ReportReceivingDataArrivalAcknowledge(DataFrame df);

        void SetupIfNotWorking();
        void GentleShutdown();
        void Destroy();
    }
}
