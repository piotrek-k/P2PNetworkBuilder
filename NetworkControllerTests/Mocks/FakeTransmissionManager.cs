using Microsoft.Extensions.Logging;
using NetworkController;
using NetworkController.DataTransferStructures;
using NetworkController.DataTransferStructures.Other;
using NetworkController.Interfaces;
using NetworkController.Models;
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

        AckStatus? _statusOfSentMessage = null;

        public FakeTransmissionManager(FakeNetworkBuilder fnb, ILogger logger)
        {
            _logger = logger;
            _fnb = fnb;
        }

        protected override void EventOnAddToDataFrameQueue((WaitingMessage, Action<AckStatus>) item)
        {
            _fnb.GlobalMessageQueue.Enqueue((item.Item1, item.Item2, this));
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
            //_waitingCallbacks.Enqueue((callback, receivedPayload));
            //callback?.Invoke((AckStatus)receivedPayload.Status);
            if(_statusOfSentMessage != null)
            {
                throw new Exception("Ack sent twice");
            }
            else
            {
                _statusOfSentMessage = (AckStatus)receivedPayload.Status;
            }
        }

        public override void SendFrameAndForget(DataFrame df, IPEndPoint destination)
        {
            _fnb.SimulateSendingMessage(df, destination);
        }

        public override void SendFrameEnsureDelivered(DataFrame df, IPEndPoint destination, Action<AckStatus> callback = null)
        {
            base.SendFrameEnsureDelivered(df, destination, callback);
        }

        public override void SetupIfNotWorking(uint startingValue, IExternalNode node = null)
        {
            base.SetupIfNotWorking(startingValue, node);
        }

        public bool ProcessNextMessage(WaitingMessage waitingMessage, Action<AckStatus> callback)
        {
            if (_dataframeQueue.Count > 0)
            {
                //(var waitingMessage, var callback) = _dataframeQueue.Dequeue();
                var df = waitingMessage.DataFrame;
                var destination = waitingMessage.Destination;

                _statusOfSentMessage = null;

                try
                {
                    _logger.LogInformation($"FTM: SENDING FROM {df.SourceNodeIdGuid} " +
                        $"{Enum.GetName(typeof(MessageType), waitingMessage.DataFrame.MessageType)} " +
                        $" ret. id: {waitingMessage.DataFrame.RetransmissionId} " +
                        $" ");

                    _fnb.SimulateSendingMessage(df, destination, callback);
                }
                finally
                {
                    if(_statusOfSentMessage == null)
                    {
                        throw new Exception("Ack not sent");
                    }
                    _logger.LogInformation($"FTM: {df.SourceNodeIdGuid} CALLBACK OF " +
                        $"{Enum.GetName(typeof(MessageType), waitingMessage.DataFrame.MessageType)}");
                    callback?.Invoke(_statusOfSentMessage.Value);
                    // run all waiting callbacks
                    //while (_waitingCallbacks.Count > 0)
                    //{
                    //    var waitingCallback = _waitingCallbacks.Dequeue();
                    //    var cb = waitingCallback.Item1;
                    //    var receiveAck = waitingCallback.Item2;

                    //    cb?.Invoke((AckStatus)receiveAck.Status);

                    //    _logger.LogInformation($"FTM: EXECUTING CALLBACK");
                    //}
                }

                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
