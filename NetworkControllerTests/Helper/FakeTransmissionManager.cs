using Microsoft.Extensions.Logging;
using NetworkController;
using NetworkController.DataTransferStructures;
using NetworkController.DataTransferStructures.Other;
using NetworkController.Interfaces;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace NetworkControllerTests.Helper
{
    public class FakeTransmissionManager : ITransmissionManager
    {
        private FakeNetworkBuilder _fnb;
        private uint currentSendingId = 1;
        private uint allSentMessagesCounter = 1;
        private ILogger _logger;
        private Queue<(WaitingMessage, Action<AckStatus>)> _dataframeQueue = new Queue<(WaitingMessage, Action<AckStatus>)>();

        private Queue<(Action<AckStatus>, ReceiveAcknowledge)> _waitingCallbacks = new Queue<(Action<AckStatus>, ReceiveAcknowledge)>();

        class WaitingMessage
        {
            public DataFrame DataFrame { get; set; }
            public IPEndPoint Destination { get; set; }
        }

        public FakeTransmissionManager(FakeNetworkBuilder fnb, ILogger logger)
        {
            _logger = logger;
            _fnb = fnb;
        }

        public void Destroy()
        {
            //throw new NotImplementedException();
        }

        public void GentleShutdown()
        {
            //throw new NotImplementedException();
        }

        public void ReportReceivingDataArrivalAcknowledge(DataFrame df, ReceiveAcknowledge receivedPayload)
        {
            if (df.RetransmissionId == currentSendingId)
            {
                if (_dataframeQueue.Count > 0)
                {
                    (WaitingMessage wm, Action<AckStatus> callback) = _dataframeQueue.Dequeue();
                    //callback?.Invoke((AckStatus)receivedPayload.Status);
                    // callbacks cannot be deployed in sequential environment
                    _waitingCallbacks.Enqueue((callback, receivedPayload));

                    if (currentSendingId != uint.MaxValue)
                    {
                        currentSendingId++;
                    }
                    else
                    {
                        currentSendingId = 1;
                    }
                }
            }
            else
            {
                _logger.LogError($"Received incorrect retransmission id. {df.RetransmissionId} (received) vs {currentSendingId} (wanted)");
            }
        }

        public void SendFrameAndForget(DataFrame df, IPEndPoint destination)
        {
            _fnb.HandleMessage(df, destination);
        }

        public void SendFrameEnsureDelivered(DataFrame df, IPEndPoint destination, Action<AckStatus> callback = null)
        {
            df.RetransmissionId = allSentMessagesCounter;

            if (allSentMessagesCounter != uint.MaxValue)
            {
                allSentMessagesCounter++;
            }
            else
            {
                allSentMessagesCounter = 1;
            }

            var waitingMessage = new WaitingMessage()
            {
                DataFrame = df,
                Destination = destination
            };

            _dataframeQueue.Enqueue((waitingMessage, callback));

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

        public void SetupIfNotWorking(uint startingValue, IExternalNode node = null)
        {
            allSentMessagesCounter = startingValue;
            currentSendingId = startingValue;
        }
    }
}
