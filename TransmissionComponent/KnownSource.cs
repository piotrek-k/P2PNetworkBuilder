using System;
using System.Collections.Generic;
using System.Net;
using TransmissionComponent.Structures;

namespace TransmissionComponent
{
    public class KnownSource
    {
        public uint NextExpectedIncomingMessageId = 1;
        private ExtendedUdpClient _euc;

        public class WaitingMessage
        {
            public DataFrame DataFrame { get; set; }
            public IPEndPoint Sender { get; set; }
        }

        public SortedList<uint, WaitingMessage> WaitingMessages { get; private set; } = new SortedList<uint, WaitingMessage>();

        public KnownSource(ExtendedUdpClient euc)
        {
            _euc = euc;
        }

        public void HandleNewMessage(IPEndPoint senderIpEndPoint, DataFrame df)
        {
            if (df.RetransmissionId == NextExpectedIncomingMessageId)
            {
                _euc.OnNewMessageReceived(new NewMessageEventArgs
                {
                    DataFrame = df
                });
            }
            else
            {
                WaitingMessages.Add(df.RetransmissionId, new WaitingMessage
                {
                    DataFrame = df,
                    Sender = senderIpEndPoint
                });
            }
        }
    }
}