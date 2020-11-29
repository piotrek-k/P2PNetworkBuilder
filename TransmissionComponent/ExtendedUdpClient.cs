using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using TransmissionComponent.Structures;
using TransmissionComponent.Structures.Other;

namespace TransmissionComponent
{
    public class ExtendedUdpClient : ITransmissionHandler
    {
        IUdpClient udpClient;

        public Dictionary<uint, TrackedMessage> TrackedMessages { get; private set; } = new Dictionary<uint, TrackedMessage>();
        private object trackedMessagesLock = new object();

        public uint NextSentMessageId = 0;
        public uint WaitingTimeBetweenRetransmissions { get; set; } = 2000;

        public ExtendedUdpClient()
        {
            udpClient = new UdpClientAdapter();
        }

        public ExtendedUdpClient(IUdpClient udpClientInstance) : this()
        {
            udpClient = udpClientInstance;
        }

        public void SendMessageSequentially(IPEndPoint endPoint, int messageType, byte[] payload, Guid source, byte[] encryptionSeed, Action<AckStatus> callback = null)
        {
            var dataFrame = new DataFrame
            {
                MessageType = messageType,
                Payload = payload,
                PayloadSize = payload != null ? payload.Length : 0,
                SourceNodeIdGuid = source,
                ExpectAcknowledge = true,
                RetransmissionId = NextSentMessageId,
                IV = encryptionSeed
            };
            var bytes = dataFrame.PackToBytes();

            udpClient.Send(bytes, bytes.Length, endPoint);

            TrackedMessage tm = new TrackedMessage(bytes, endPoint);
            TrackedMessages.Add(NextSentMessageId, tm);

            Thread thread = new Thread(() => RetransmissionThread(NextSentMessageId, tm));
            thread.IsBackground = true;
            thread.Start();

            NextSentMessageId++;
        }

        public void RetransmissionThread(uint messageId, TrackedMessage tm)
        {
            bool messageReceived = false;
            do
            {
                lock (tm.ThreadLock)
                {
                    Monitor.Wait(tm.ThreadLock, checked((int)WaitingTimeBetweenRetransmissions));
                }

                lock (trackedMessagesLock)
                {
                    messageReceived = !TrackedMessages.ContainsKey(messageId);

                    if (messageReceived)
                        break;
                }

                udpClient.Send(tm.Contents, tm.Contents.Length, tm.Endpoint);

            } while (messageReceived);
        }
    }
}
