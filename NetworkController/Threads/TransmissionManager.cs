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
    public class TransmissionManager : ITransmissionManager
    {
        class WaitingMessage
        {
            public DataFrame DataFrame { get; set; }
            public IPEndPoint Destination { get; set; }
        }

        private INetworkControllerInternal _networkController;
        private readonly IExternalNodeInternal _externalNode;
        private ILogger _logger;
        private object counterAndQueueLock = new object();
        private Queue<(WaitingMessage, Action<AckStatus>)> _dataframeQueue = new Queue<(WaitingMessage, Action<AckStatus>)>();
        private uint currentSendingId = 1;
        private uint allSentMessagesCounter = 1;
        private object threadCheckLock = new object();
        private object retransmissionSleepLock = new object();

        const short MsBetweenRetransmissions = 2000;

        Thread retransmissionThread = null;

        private CancellationTokenSource _cancelThreadSource;
        private CancellationToken _cancelThread;

        public TransmissionManager(INetworkControllerInternal networkController, IExternalNodeInternal externalNode, ILogger logger)
        {
            _networkController = networkController;
            _externalNode = externalNode;
            _logger = logger;

            SetupIfNotWorking();
        }

        public void SetupIfNotWorking()
        {
            if (retransmissionThread == null)
            {
                _cancelThreadSource = new CancellationTokenSource();
                _cancelThread = _cancelThreadSource.Token;

                retransmissionThread = new Thread(RetransmissionLoop);
                //retransmissionThread.IsBackground = true;
                retransmissionThread.Start();
            }
        }

        public void GentleShutdown()
        {
            //retransmissionThread.Abort();
            _cancelThreadSource.Cancel();
            retransmissionThread = null;
        }

        public void Destroy()
        {
            //retransmissionThread.Abort();
            // temporal solution
            GentleShutdown();
            _logger.LogDebug("Transmission manager destroyed");
        }

        public void SendFrameEnsureDelivered(DataFrame df, IPEndPoint destination, Action<AckStatus> callback = null)
        {
            if (retransmissionThread == null)
            {
                throw new Exception("Bad behaviour: sending data while TransmissionManager shut down");
            }

            lock (counterAndQueueLock)
            {
                df.RetransmissionId = allSentMessagesCounter;

                if(allSentMessagesCounter != uint.MaxValue)
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
                Monitor.Pulse(counterAndQueueLock);
            }
        }

        public void SendFrameAndForget(DataFrame df, IPEndPoint destination)
        {
            _networkController.SendBytes(df.PackToBytes(), destination);
        }

        public void ReportReceivingDataArrivalAcknowledge(DataFrame df, ReceiveAcknowledge receivedPayload)
        {
            if (retransmissionThread == null)
            {
                throw new Exception("Bad behaviour: sending data while TransmissionManager shut down");
            }

            lock (counterAndQueueLock)
            {
                if (df.RetransmissionId == currentSendingId)
                {
                    (WaitingMessage wm, Action<AckStatus> callback) = _dataframeQueue.Dequeue();
                    callback?.Invoke((AckStatus)receivedPayload.Status);

                    if (currentSendingId != uint.MaxValue)
                    {
                        currentSendingId++;
                    }
                    else
                    {
                        currentSendingId = 1;
                    }

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
                while (idOfRetransmittedMessage == currentSendingId)
                {
                    if (!firstTransmission)
                    {
                        _logger.LogDebug($"{_externalNode.Id} Retransmission of {wm.DataFrame.RetransmissionId}");
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

        ~TransmissionManager()
        {
            Destroy();
        }
    }
}
