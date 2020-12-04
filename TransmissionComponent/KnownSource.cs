using System;
using System.Collections.Generic;
using System.Net;
using TransmissionComponent.Structures;
using TransmissionComponent.Structures.Other;

namespace TransmissionComponent
{
    public class KnownSource
    {
        public int NextExpectedIncomingMessageId = 1;
        private ExtendedUdpClient _euc;
        private Guid _deviceId;

        public class WaitingMessage
        {
            public DataFrame DataFrame { get; set; }
            public IPEndPoint Sender { get; set; }
        }

        public SortedList<int, WaitingMessage> WaitingMessages { get; private set; } = new SortedList<int, WaitingMessage>();
        public HashSet<int> ProcessedMessages { get; private set; } = new HashSet<int>();

        public KnownSource(ExtendedUdpClient euc, Guid deviceId)
        {
            _euc = euc;
            _deviceId = deviceId;
        }

        public void HandleNewMessage(IPEndPoint senderIpEndPoint, DataFrame df)
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
                DataFrame = df
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
                wm = WaitingMessages.GetValueOrDefault(NextExpectedIncomingMessageId);

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