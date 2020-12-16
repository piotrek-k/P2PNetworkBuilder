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
        int _nextExpectedIncomingMessageId = 1;
        internal int NextExpectedIncomingMessageId
        {
            get
            {
                lock (IncomingMessageCounterLock)
                {
                    return _nextExpectedIncomingMessageId;
                }
            }
            private set
            {
                lock (IncomingMessageCounterLock)
                {
                    _nextExpectedIncomingMessageId = value;
                }
            }
        }
        internal readonly object IncomingMessageCounterLock = new object();

        int _nextIdForMessageToSend = 1;
        internal int NextIdForMessageToSend
        {
            get
            {
                lock (OutgoingMessageCounterLock)
                {
                    return _nextIdForMessageToSend;
                }
            }
            private set
            {
                lock (OutgoingMessageCounterLock)
                {
                    _nextIdForMessageToSend = value;
                }
            }
        }
        internal readonly object OutgoingMessageCounterLock = new object();

        private ExtendedUdpClient _euc;
        private Guid _deviceId;
        private ILogger _logger;

        internal class WaitingMessage
        {
            public DataFrame DataFrame { get; set; }
            public IPEndPoint Sender { get; set; }
        }

        internal SortedList<int, WaitingMessage> WaitingIncomingMessages { get; private set; } = new SortedList<int, WaitingMessage>();
        internal HashSet<int> ProcessedMessages { get; private set; } = new HashSet<int>();

        /// <summary>
        /// Stores messages that were sent, but receiver haven't sent back acknowledge yet
        /// </summary>
        internal Dictionary<int, TrackedMessage> TrackedOutgoingMessages { get; private set; } = new Dictionary<int, TrackedMessage>();
        /// <summary>
        /// Lock that keeps TrackedMessages thread-safe
        /// </summary>
        internal object trackedMessagesLock = new object();

        /// <summary>
        /// In case external device didn't receive ReceiveAck. Should be cleared periodically.
        /// </summary>
        internal Dictionary<int, AckStatus> AlreadyProcessedMessagesResultRegister { get; private set; } = new Dictionary<int, AckStatus>();

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
            if (df.ExpectAcknowledge)
            {
                if (df.SendSequentially)
                {
                    // sequential message

                    if (df.RetransmissionId == NextExpectedIncomingMessageId)
                    {
                        // can be processed straight away

                        ProcessMessageAndSendAck(df, senderIpEndPoint);

                        SetExpectedIncomingMessageCounterToNextValue();
                    }
                    else if ((NextExpectedIncomingMessageId > 0 && df.RetransmissionId > NextExpectedIncomingMessageId) ||
                        (NextExpectedIncomingMessageId < 0 && df.RetransmissionId < NextExpectedIncomingMessageId))
                    {
                        // received too early. Processing needs to be postponed

                        _logger.LogTrace($"Received {df.RetransmissionId} but expected {NextExpectedIncomingMessageId}. Message postponed.");
                        try
                        {
                            WaitingIncomingMessages.Add(df.RetransmissionId, new WaitingMessage
                            {
                                DataFrame = df,
                                Sender = senderIpEndPoint
                            });
                        }
                        catch (ArgumentException)
                        {
                            // seems to be a retransmission
                            _logger.LogWarning("ArgumentException while adding incoming message to waiting list");
                        }
                    }
                    else
                    {
                        // already processed. Probaby ReceiveAck wasn't delivered properly

                        _logger.LogTrace($"Message with retransmission id {df.RetransmissionId} already processed. Sending ReceiveAck again. ");
                        TryToSendReceiveAckAgain(senderIpEndPoint, df.RetransmissionId);
                    }
                }
                else
                {
                    // non-sequential message

                    if (
                        (
                            (NextExpectedIncomingMessageId > 0 && df.RetransmissionId >= NextExpectedIncomingMessageId) ||
                            (NextExpectedIncomingMessageId < 0 && df.RetransmissionId <= NextExpectedIncomingMessageId)
                        )
                        && !ProcessedMessages.Contains(df.RetransmissionId))
                    {
                        // Non-sequential message with proper id

                        ProcessMessageAndSendAck(df, senderIpEndPoint);

                        if (df.RetransmissionId == NextExpectedIncomingMessageId)
                        {
                            SetExpectedIncomingMessageCounterToNextValue();
                        }
                        else
                        {
                            ProcessedMessages.Add(df.RetransmissionId);
                        }
                    }
                    else
                    {
                        // Non-sequential message with outdated id?

                        var result = TryToSendReceiveAckAgain(senderIpEndPoint, df.RetransmissionId);

                        if (!result)
                        {
                            // not processed with outdated id

                            _logger.LogError($"Recevied incorrect message id {df.RetransmissionId}");
                        }
                    }
                }
            }
            else if (df.RetransmissionId == 0 && !df.ExpectAcknowledge)
            {
                // Message sent by SendAndForget method

                ProcessMessage(df, senderIpEndPoint);
            }
            else
            {
                _logger.LogError($"Don't know how to handle this message {df.RetransmissionId}");
            }
        }

        private bool TryToSendReceiveAckAgain(IPEndPoint senderIpEndPoint, int retransmissionId)
        {
            AckStatus foundResult;
            if (AlreadyProcessedMessagesResultRegister.TryGetValue(retransmissionId, out foundResult))
            {
                SendReceiveAcknowledge(retransmissionId, senderIpEndPoint, foundResult);
                return true;
            }
            else
            {
                _logger.LogError($"Couldn't find requested message result ({retransmissionId})");
                return false;
            }
        }

        private void ProcessMessageAndSendAck(DataFrame df, IPEndPoint callbackEndpoint)
        {
            AckStatus result = ProcessMessage(df, callbackEndpoint);
            SendReceiveAcknowledge(df.RetransmissionId, callbackEndpoint, result);

            try
            {
                AlreadyProcessedMessagesResultRegister.Add(df.RetransmissionId, result);
            }
            catch (ArgumentException)
            {
                _logger.LogWarning("System tried to add the same ReceiveAck more than once");
            }
        }

        private void SendReceiveAcknowledge(int retransmissionId, IPEndPoint callbackEndpoint, AckStatus result)
        {
            var ra = new ReceiveAcknowledge()
            {
                Status = (int)result
            }.PackToBytes();

            _euc.SendReceiveAck(callbackEndpoint, ra, _euc.DeviceId, retransmissionId);
        }

        private AckStatus ProcessMessage(DataFrame df, IPEndPoint callbackEndpoint)
        {
            return _euc.OnNewMessageReceived(new NewMessageEventArgs
            {
                DataFrame = df,
                SenderIPEndpoint = callbackEndpoint
            });
        }

        private void SetExpectedIncomingMessageCounterToNextValue()
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

        internal void SetIdForMessageToSendToNextValue()
        {
            checked
            {
                try
                {
                    if (NextIdForMessageToSend > 0)
                    {
                        NextIdForMessageToSend += 1;
                    }
                    else
                    {
                        NextIdForMessageToSend -= 1;
                    }
                }
                catch (OverflowException)
                {
                    if (NextIdForMessageToSend > 0)
                    {
                        NextIdForMessageToSend = 1;
                    }
                    else
                    {
                        NextIdForMessageToSend = -1;
                    }
                }
            }
        }

        private void SequentiallyProcessNextWaitingMessages()
        {
            WaitingMessage wm;
            bool shouldContinue;
            do
            {
                shouldContinue = false;
                WaitingIncomingMessages.TryGetValue(NextExpectedIncomingMessageId, out wm);

                if (wm != null)
                {
                    ProcessMessageAndSendAck(wm.DataFrame, wm.Sender);

                    SetExpectedIncomingMessageCounterToNextValue();

                    shouldContinue = true;
                }
                else if (ProcessedMessages.Contains(NextExpectedIncomingMessageId))
                {
                    ProcessedMessages.Remove(NextExpectedIncomingMessageId);

                    SetExpectedIncomingMessageCounterToNextValue();

                    shouldContinue = true;
                }

            } while (shouldContinue);
        }

        public void ResetIncomingMessagesCounter(int expectedNextIncomingValue)
        {
            WaitingIncomingMessages.Clear();
            ProcessedMessages.Clear();

            NextExpectedIncomingMessageId = expectedNextIncomingValue;
        }

        public void ResetOutgoingMessagesCounter(int expectedNextOutgoingMessageId)
        {
            lock (trackedMessagesLock)
            {
                TrackedOutgoingMessages.Clear();
            }

            NextIdForMessageToSend = expectedNextOutgoingMessageId;
        }
    }
}