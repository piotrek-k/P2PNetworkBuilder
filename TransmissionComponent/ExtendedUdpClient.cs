using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using TransmissionComponent.Structures;
using TransmissionComponent.Structures.Other;

namespace TransmissionComponent
{
    public class ExtendedUdpClient : ITransmissionHandler
    {
        private IUdpClient udpClient;
        private ILogger _logger;

        public uint WaitingTimeBetweenRetransmissions { get; set; } = 2000;

        public Dictionary<uint, TrackedMessage> TrackedMessages { get; private set; } = new Dictionary<uint, TrackedMessage>();
        private object trackedMessagesLock = new object();

        public uint NextSentMessageId = 0;
        
        public Dictionary<Guid, KnownSource> KnownSources = new Dictionary<Guid, KnownSource>();

        public event EventHandler<NewMessageEventArgs> NewIncomingMessage;
        public virtual void OnNewMessageReceived(NewMessageEventArgs e)
        {
            EventHandler<NewMessageEventArgs> handler = NewIncomingMessage;
            handler?.Invoke(this, e);
        }

        public ExtendedUdpClient(ILogger logger)
        {
            udpClient = null;
            _logger = logger;
        }

        public ExtendedUdpClient(IUdpClient udpClientInstance, ILogger logger) : this(logger)
        {
            udpClient = udpClientInstance;
        }

        public void StartListening(int port = 13000)
        {
            udpClient = new UdpClientAdapter(port);

            // Related to closing "UDP connection" issue
            const int SIO_UDP_CONNRESET = -1744830452;
            try
            {
                udpClient.Client.IOControl(
                    (IOControlCode)SIO_UDP_CONNRESET,
                    new byte[] { 0, 0, 0, 0 },
                    null
                );
            }
            catch (Exception)
            {
                _logger.LogTrace("Couldn't set IOControl. Ignore if not working on Windows");
            }

            udpClient.BeginReceive(new AsyncCallback(HandleIncomingMessages), null);
        }

        public void HandleIncomingMessages(IAsyncResult ar)
        {
            IPEndPoint senderIpEndPoint = new IPEndPoint(0, 0);
            var receivedData = udpClient.EndReceive(ar, ref senderIpEndPoint);
            DataFrame df = DataFrame.Unpack(receivedData);

            KnownSource foundSource = null;
            KnownSources.TryGetValue(df.SourceNodeIdGuid, out foundSource);

            if (foundSource == null)
            {
                foundSource = new KnownSource(this);
                KnownSources.Add(df.SourceNodeIdGuid, foundSource);
            }
            else
            {
                foundSource.HandleNewMessage(senderIpEndPoint, df);
            }
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
