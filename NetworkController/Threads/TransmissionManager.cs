using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Microsoft.Extensions.Logging;
using NetworkController.DataTransferStructures;
using NetworkController.DataTransferStructures.Other;
using NetworkController.Interfaces;
using NetworkController.Interfaces.ForTesting;
using Newtonsoft.Json.Converters;

namespace NetworkController.Threads
{
    public class TransmissionManager : TransmissionManagerBase, ITransmissionManager
    {
        private INetworkControllerInternal _networkController;
        private readonly IExternalNodeInternal _externalNode;
        private ILogger _logger;
        private object counterAndQueueLock = new object();
        private object threadCheckLock = new object();
        private object retransmissionSleepLock = new object();

        const short MsBetweenRetransmissions = 2000;

        Thread retransmissionThread = null;

        private CancellationTokenSource _cancelThreadSource;
        private CancellationToken _cancelThread;

        public TransmissionManager(INetworkControllerInternal networkController, IExternalNodeInternal externalNode, ILogger logger, uint startingValue = 1)
        {
            _networkController = networkController;
            _externalNode = externalNode;
            _logger = logger;

            SetupIfNotWorking(startingValue);
        }

        public override void SetupIfNotWorking(uint startingValue, IExternalNode node = null)
        {
            if (retransmissionThread == null)
            {
                base.SetupIfNotWorking(startingValue, node);

                _cancelThreadSource = new CancellationTokenSource();
                _cancelThread = _cancelThreadSource.Token;

                retransmissionThread = new Thread(RetransmissionLoop);
                //retransmissionThread.IsBackground = true;
                retransmissionThread.Start();
            }
        }

        public override void GentleShutdown()
        {
            //retransmissionThread.Abort();
            _cancelThreadSource.Cancel();
            retransmissionThread = null;
        }

        public override void Destroy()
        {
            //retransmissionThread.Abort();
            // temporal solution
            GentleShutdown();
            _logger.LogTrace("Transmission manager destroyed");
        }

        public override void SendFrameEnsureDelivered(DataFrame df, IPEndPoint destination, Action<AckStatus> callback = null)
        {
            if (retransmissionThread == null)
            {
                throw new Exception("Bad behaviour: sending data while TransmissionManager shut down");
            }

            lock (counterAndQueueLock)
            {
                base.SendFrameEnsureDelivered(df, destination, callback);

                Monitor.Pulse(counterAndQueueLock);
            }
        }

        public override void SendFrameAndForget(DataFrame df, IPEndPoint destination)
        {
            _networkController.SendBytes(df.PackToBytes(), destination);
        }

        public override void ReportReceivingDataArrivalAcknowledge(DataFrame df, ReceiveAcknowledge receivedPayload)
        {
            if (retransmissionThread == null)
            {
                throw new Exception("Bad behaviour: sending data while TransmissionManager shut down");
            }

            lock (counterAndQueueLock)
            {
                if (df.RetransmissionId == currentSendingId)
                {
                    base.HandleMessageWithCorrectId(receivedPayload);

                    lock (retransmissionSleepLock)
                    {
                        Monitor.Pulse(retransmissionSleepLock);
                    }
                    Monitor.Pulse(counterAndQueueLock);
                }
                else
                {
                    _logger.LogError($"Received incorrect retransmission id. {df.RetransmissionId} (received) vs {currentSendingId} (wanted)");
                }
            }
        }

        protected override void HandleAckCallback(Action<AckStatus> callback, ReceiveAcknowledge receivedPayload)
        {
            callback?.Invoke((AckStatus)receivedPayload.Status);
        }

        /// <summary>
        /// Thread retransmitting message as long as currentSendingId doesn't change.
        /// Thread is suspended when no messages in queue.
        /// </summary>
        private void RetransmissionLoop()
        {
            while (!_cancelThread.IsCancellationRequested)
            {
                WaitingMessage wm = null;
                uint idOfRetransmittedMessage;

                lock (counterAndQueueLock)
                {
                    while (_dataframeQueue.Count == 0)
                    {
                        Monitor.Wait(counterAndQueueLock);
                    }

                    if (_cancelThread.IsCancellationRequested)
                    {
                        break;
                    }

                    Action<AckStatus> cb;
                    (wm, cb) = _dataframeQueue.Peek();
                    idOfRetransmittedMessage = currentSendingId;
                    //wm.DataFrame.RetransmissionId = currentSendingId;
                    if (idOfRetransmittedMessage != wm.DataFrame.RetransmissionId)
                    {
                        _logger.LogError("Incorrect retransmission id in RetransmissionLoop");
                    }
                }

                bool firstTransmission = true;
                short failsCounter = 0;
                while (idOfRetransmittedMessage == currentSendingId)
                {
                    if (!firstTransmission)
                    {
                        _logger.LogTrace($"{_externalNode.Id} Retransmission of {wm.DataFrame.RetransmissionId}");
                        failsCounter++;
                    }
                    firstTransmission = false;

                    _networkController.SendBytes(wm.DataFrame.PackToBytes(), wm.Destination);
                    lock (retransmissionSleepLock)
                    {
                        if (_externalNode.CurrentState == UDP.ExternalNode.ConnectionState.Failed)
                        {
                            _logger.LogDebug($"{_externalNode.Id} Retransmission suspended as state is 'Failed'");
                            Monitor.Wait(retransmissionSleepLock);
                        }
                        else if (failsCounter > 5 && _externalNode.CurrentState != UDP.ExternalNode.ConnectionState.Ready)
                        {
                            // in case of "Ready" state, retransmission will be shut down by KeepaliveThread
                            // in other cases, we need to handle it here:
                            _externalNode.ReportConnectionFailure();
                            failsCounter = 0;
                        }
                        else
                        {
                            Monitor.Wait(retransmissionSleepLock, MsBetweenRetransmissions);
                        }
                    }

                    if (_cancelThread.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }
        }

        protected override void EventOnAddToDataFrameQueue((WaitingMessage, Action<AckStatus>) item)
        {
            //throw new NotImplementedException();
        }

        ~TransmissionManager()
        {
            Destroy();
        }
    }
}
