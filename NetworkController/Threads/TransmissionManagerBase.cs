using NetworkController.DataTransferStructures;
using NetworkController.DataTransferStructures.Other;
using NetworkController.Interfaces;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace NetworkController.Threads
{
    public abstract class TransmissionManagerBase : ITransmissionManager
    {
        protected uint currentSendingId = 1;
        protected uint allSentMessagesCounter = 1;
        protected Queue<(WaitingMessage, Action<AckStatus>)> _dataframeQueue = new Queue<(WaitingMessage, Action<AckStatus>)>();
        protected class WaitingMessage
        {
            public DataFrame DataFrame { get; set; }
            public IPEndPoint Destination { get; set; }
        }

        public abstract void Destroy();

        public abstract void GentleShutdown();

        public abstract void ReportReceivingDataArrivalAcknowledge(DataFrame df, ReceiveAcknowledge receivedPayload);

        /// <summary>
        /// Used when sent message contains callback and it's time to run it
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="receivedPayload"></param>
        protected abstract void HandleAckCallback(Action<AckStatus> callback, ReceiveAcknowledge receivedPayload);
        /// <summary>
        /// Used if transmission manager receives message and it's correct (retransmision id matches id of message that was just sent)
        /// </summary>
        /// <param name="receivedPayload"></param>
        protected virtual void HandleMessageWithCorrectId(ReceiveAcknowledge receivedPayload)
        {
            if (_dataframeQueue.Count > 0)
            {
                (WaitingMessage wm, Action<AckStatus> callback) = _dataframeQueue.Dequeue();
                
                HandleAckCallback(callback, receivedPayload);

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

        public abstract void SendFrameAndForget(DataFrame df, IPEndPoint destination);
        
        public virtual void SendFrameEnsureDelivered(DataFrame df, IPEndPoint destination, Action<AckStatus> callback = null)
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
        }

        public virtual void SetupIfNotWorking(uint startingValue, IExternalNode node = null)
        {
            allSentMessagesCounter = startingValue;
            currentSendingId = startingValue;
        }
    }
}
