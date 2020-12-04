using System;
using System.Collections.Generic;
using System.Net;
using TransmissionComponent.Structures;

namespace TransmissionComponent
{
    public class KnownSource
    {
        public int NextExpectedIncomingMessageId = 1;
        private ExtendedUdpClient _euc;

        public class WaitingMessage
        {
            public DataFrame DataFrame { get; set; }
            public IPEndPoint Sender { get; set; }
        }

        public SortedList<int, WaitingMessage> WaitingMessages { get; private set; } = new SortedList<int, WaitingMessage>();
        public HashSet<int> ProcessedMessages { get; private set; } = new HashSet<int>();

        public KnownSource(ExtendedUdpClient euc)
        {
            _euc = euc;
        }

        public void HandleNewMessage(IPEndPoint senderIpEndPoint, DataFrame df)
        {
            if (df.SendSequentially)
            {
                if (df.RetransmissionId == NextExpectedIncomingMessageId)
                {
                    _euc.OnNewMessageReceived(new NewMessageEventArgs
                    {
                        DataFrame = df
                    });

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
                _euc.OnNewMessageReceived(new NewMessageEventArgs
                {
                    DataFrame = df
                });

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
                    _euc.OnNewMessageReceived(new NewMessageEventArgs
                    {
                        DataFrame = wm.DataFrame
                    });

                    NextExpectedIncomingMessageId += 1;

                    shouldContinue = true;
                }
                else if (ProcessedMessages.Contains(NextExpectedIncomingMessageId))
                {
                    ProcessedMessages.Remove(NextExpectedIncomingMessageId);
                    NextExpectedIncomingMessageId++;
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