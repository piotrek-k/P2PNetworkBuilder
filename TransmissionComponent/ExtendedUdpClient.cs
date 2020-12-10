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

        /// <summary>
        /// Stores messages that were sent, but receiver haven't sent back acknowledge yet
        /// </summary>
        internal Dictionary<int, TrackedMessage> TrackedMessages { get; private set; } = new Dictionary<int, TrackedMessage>();
        /// <summary>
        /// Lock that keeps TrackedMessages thread-safe
        /// </summary>
        internal object trackedMessagesLock = new object();

        /// <summary>
        /// Id that will be assigned to next sent message
        /// </summary>
        internal int NextSentMessageId = 0;

        /// <summary>
        /// Stores `KnownSource` class instance for each Guid that ever appeared in any incoming message
        /// </summary>
        internal Dictionary<Guid, KnownSource> KnownSources = new Dictionary<Guid, KnownSource>();

        /// <summary>
        /// Function that will be executed each time message arrives (after ordering and filtration done by component)
        /// </summary>
        public Func<NewMessageEventArgs, AckStatus> NewIncomingMessage { get; set; }
        public virtual AckStatus OnNewMessageReceived(NewMessageEventArgs e)
        {
            if (NewIncomingMessage != null)
            {
                return NewIncomingMessage.Invoke(e);
            }

            throw new Exception("Message not processed. No listener.");
        }

        public ExtendedUdpClient(ILogger logger)
        {
            _logger = logger;
        }

        public ExtendedUdpClient(IUdpClient udpClientInstance, ILogger logger) : this(logger)
        {
            udpClient = udpClientInstance;
        }

        /// <summary>
        /// Begins listening for incoming messages on specified port
        /// </summary>
        /// <param name="port"></param>
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

        internal void HandleIncomingMessages(IAsyncResult ar)
        {
            IPEndPoint senderIpEndPoint = new IPEndPoint(0, 0);
            var receivedData = udpClient.EndReceive(ar, ref senderIpEndPoint);
            DataFrame df = DataFrame.Unpack(receivedData);

            if (df.ReceiveAck)
            {
                lock (trackedMessagesLock)
                {
                    TrackedMessage tm;
                    AckStatus status = AckStatus.Failure; // failure by default

                    TrackedMessages.TryGetValue(df.RetransmissionId, out tm);

                    if (df.Payload != null)
                    {
                        try
                        {
                            ReceiveAcknowledge ra = ReceiveAcknowledge.Unpack(df.Payload);
                            status = (AckStatus)ra.Status;
                        }
                        catch (Exception e)
                        {
                            _logger.LogError($"Error while handling ReceiveAck. {e.Message}");
                        }
                    }

                    if(tm != null && tm.Callback != null)
                    {
                        tm.Callback(status);
                    }

                    TrackedMessages.Remove(df.RetransmissionId);
                }
            }
            else
            {
                KnownSource foundSource;
                KnownSources.TryGetValue(df.SourceNodeIdGuid, out foundSource);

                if (foundSource == null)
                {
                    foundSource = new KnownSource(this, df.SourceNodeIdGuid);
                    KnownSources.Add(df.SourceNodeIdGuid, foundSource);
                }

                foundSource.HandleNewMessage(senderIpEndPoint, df);
            }
        }

        /// <summary>
        /// Send message ensuring it will be delivered (retransmitted if necessary) and processed in correct order
        /// </summary>
        /// <param name="endPoint">address to which message should be sent</param>
        /// <param name="messageType"></param>
        /// <param name="payload"></param>
        /// <param name="source">Guid of this device</param>
        /// <param name="encryptionSeed"></param>
        /// <param name="callback">method that should be called after successfull delivery</param>
        public void SendMessageSequentially(IPEndPoint endPoint, byte[] payload, Guid source, Action<AckStatus> callback = null)
        {
            var dataFrame = new DataFrame
            {
                Payload = payload,
                PayloadSize = payload != null ? payload.Length : 0,
                SourceNodeIdGuid = source,
                ExpectAcknowledge = true,
                RetransmissionId = NextSentMessageId
            };
            var bytes = dataFrame.PackToBytes();

            udpClient.Send(bytes, bytes.Length, endPoint);

            TrackedMessage tm = new TrackedMessage(bytes, endPoint, callback);
            TrackedMessages.Add(NextSentMessageId, tm);

            Thread thread = new Thread(() => RetransmissionThread(NextSentMessageId, tm));
            thread.IsBackground = true;
            thread.Start();

            NextSentMessageId++;
        }

        /// <summary>
        /// Send message without checking if was delivered
        /// </summary>
        /// <param name="endPoint"></param>
        /// <param name="messageType"></param>
        /// <param name="payload"></param>
        /// <param name="source"></param>
        public void SendMessageNoTracking(IPEndPoint endPoint, byte[] payload, Guid source)
        {
            var dataFrame = new DataFrame
            {
                Payload = payload,
                PayloadSize = payload != null ? payload.Length : 0,
                SourceNodeIdGuid = source,
                ExpectAcknowledge = false,
                RetransmissionId = 0
            };
            var bytes = dataFrame.PackToBytes();

            udpClient.Send(bytes, bytes.Length, endPoint);
        }

        /// <summary>
        /// Informs remote endpoint that message was processed
        /// </summary>
        /// <param name="endPoint"></param>
        /// <param name="payload"></param>
        /// <param name="source"></param>
        /// <param name="messageId"></param>
        internal void SendReceiveAck(IPEndPoint endPoint, byte[] payload, Guid source, int messageId)
        {
            var dataFrame = new DataFrame
            {
                Payload = payload,
                PayloadSize = payload != null ? payload.Length : 0,
                SourceNodeIdGuid = source,
                ExpectAcknowledge = false,
                RetransmissionId = messageId,
                ReceiveAck = true
            };
            var bytes = dataFrame.PackToBytes();

            udpClient.Send(bytes, bytes.Length, endPoint);
        }

        internal void RetransmissionThread(int messageId, TrackedMessage tm)
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
