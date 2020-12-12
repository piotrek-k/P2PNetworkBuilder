using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using TransmissionComponent.Structures;
using TransmissionComponent.Structures.Other;

namespace TransmissionComponent
{
    internal class KnownSource
    {
        internal int NextExpectedIncomingMessageId = 1;
        private ExtendedUdpClient _euc;
        private Guid _deviceId;
        private ILogger _logger;

        internal class WaitingMessage
        {
            public DataFrame DataFrame { get; set; }
            public IPEndPoint Sender { get; set; }
        }

        internal SortedList<int, WaitingMessage> WaitingMessages { get; private set; } = new SortedList<int, WaitingMessage>();
        internal HashSet<int> ProcessedMessages { get; private set; } = new HashSet<int>();

        public KnownSource(ExtendedUdpClient euc, Guid deviceId, ILogger logger)
        {
            _euc = euc;
            _deviceId = deviceId;
            _logger = logger;
        }

        /// <summary>
        /// Process message or add it to queue. Then try to process waiting messages.
        /// </summary>
        /// <param name="senderIpEndPoint"></param>
        /// <param name="df"></param>
        internal void HandleNewMessage(IPEndPoint senderIpEndPoint, DataFrame df)
        {
            if (df.SendSequentially)
            {
                if (df.RetransmissionId == NextExpectedIncomingMessageId)
                {
                    ProcessMessage(df, senderIpEndPoint);

                    SetCounterToNextValue();
                }
                else if ((NextExpectedIncomingMessageId > 0 && df.RetransmissionId > NextExpectedIncomingMessageId) ||
                    (NextExpectedIncomingMessageId < 0 && df.RetransmissionId < NextExpectedIncomingMessageId))
                {
                    WaitingMessages.Add(df.RetransmissionId, new WaitingMessage
                    {
                        DataFrame = df,
                        Sender = senderIpEndPoint
                    });
                }
            }
            else if (
                (
                    (NextExpectedIncomingMessageId > 0 && df.RetransmissionId >= NextExpectedIncomingMessageId) ||
                    (NextExpectedIncomingMessageId < 0 && df.RetransmissionId <= NextExpectedIncomingMessageId)
                )
                && !ProcessedMessages.Contains(df.RetransmissionId))
            {
                ProcessMessage(df, senderIpEndPoint);

                if (df.RetransmissionId == NextExpectedIncomingMessageId)
                {
                    SetCounterToNextValue();
                }
                else
                {
                    ProcessedMessages.Add(df.RetransmissionId);
                }
            }
        }

        private void ProcessMessage(DataFrame df, IPEndPoint callbackEndpoint)
        {
            AckStatus result = _euc.OnNewMessageReceived(new NewMessageEventArgs
            {
                DataFrame = df,
                SenderIPEndpoint = callbackEndpoint
            });

            var ra = new ReceiveAcknowledge()
            {
                Status = (int)result
            }.PackToBytes();

            _euc.SendReceiveAck(callbackEndpoint, ra, _deviceId, df.RetransmissionId);
        }

        private void SetCounterToNextValue()
        {
            checked
            {
                try
                {
                    if (NextExpectedIncomingMessageId > 0)
                    {
                        NextExpectedIncomingMessageId += 1;
                    }
                    else
                    {
                        NextExpectedIncomingMessageId -= 1;
                    }
                }
                catch (OverflowException)
                {
                    if (NextExpectedIncomingMessageId > 0)
                    {
                        NextExpectedIncomingMessageId = 1;
                    }
                    else
                    {
                        NextExpectedIncomingMessageId = -1;
                    }
                }
            }

            SequentiallyProcessNextWaitingMessages();
        }

        private void SequentiallyProcessNextWaitingMessages()
        {
            WaitingMessage wm;
            bool shouldContinue;
            do
            {
                shouldContinue = false;
                WaitingMessages.TryGetValue(NextExpectedIncomingMessageId, out wm);

                if (wm != null)
                {
                    ProcessMessage(wm.DataFrame, wm.Sender);

                    SetCounterToNextValue();

                    shouldContinue = true;
                }
                else if (ProcessedMessages.Contains(NextExpectedIncomingMessageId))
                {
                    ProcessedMessages.Remove(NextExpectedIncomingMessageId);

                    SetCounterToNextValue();
                    
                    shouldContinue = true;
                }

            } while (shouldContinue);
        }

        public void ResetCounter(int expectedNextIncomingValue)
        {
            WaitingMessages.Clear();
            ProcessedMessages.Clear();

            NextExpectedIncomingMessageId = expectedNextIncomingValue;
        }
    }
}