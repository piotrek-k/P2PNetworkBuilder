using Microsoft.Extensions.Logging;
using NetworkController;
using NetworkController.DataTransferStructures;
using NetworkController.DataTransferStructures.Other;
using NetworkController.Interfaces;
using NetworkController.Threads;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace NetworkControllerTests.Helper
{
    public class FakeTransmissionManager : TransmissionManagerBase, ITransmissionManager
    {
        private FakeNetworkBuilder _fnb;
        private ILogger _logger;

        private Queue<(Action<AckStatus>, ReceiveAcknowledge)> _waitingCallbacks = new Queue<(Action<AckStatus>, ReceiveAcknowledge)>();

        public FakeTransmissionManager(FakeNetworkBuilder fnb, ILogger logger)
        {
            _logger = logger;
            _fnb = fnb;
        }

        public override void Destroy()
        {
            //throw new NotImplementedException();
        }

        public override void GentleShutdown()
        {
            //throw new NotImplementedException();
        }

        public override void ReportReceivingDataArrivalAcknowledge(DataFrame df, ReceiveAcknowledge receivedPayload)
        {
            if (df.RetransmissionId == currentSendingId)
            {
                base.HandleMessageWithCorrectId(receivedPayload);
            }
            else
            {
                _logger.LogError($"Received incorrect retransmission id. {df.RetransmissionId} (received) vs {currentSendingId} (wanted)");
            }
        }

        protected override void HandleAckCallback(Action<AckStatus> callback, ReceiveAcknowledge receivedPayload)
        {
            _waitingCallbacks.Enqueue((callback, receivedPayload));
        }

        public override void SendFrameAndForget(DataFrame df, IPEndPoint destination)
        {
            _fnb.HandleMessage(df, destination);
        }

        public override void SendFrameEnsureDelivered(DataFrame df, IPEndPoint destination, Action<AckStatus> callback = null)
        {
            base.SendFrameEnsureDelivered(df, destination, callback);

            try
            {
                _fnb.HandleMessage(df, destination, callback);
            }
            finally
            {
                // run all waiting callbacks
                while (_waitingCallbacks.Count > 0)
                {
                    var waitingCallback = _waitingCallbacks.Dequeue();
                    var cb = waitingCallback.Item1;
                    var receiveAck = waitingCallback.Item2;

                    cb?.Invoke((AckStatus)receiveAck.Status);
                }
            }

        }

        public override void SetupIfNotWorking(uint startingValue, IExternalNode node = null)
        {
            base.SetupIfNotWorking(startingValue, node);
        }
    }
}
