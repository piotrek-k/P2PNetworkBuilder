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
        public Guid DeviceId { get; private set; }

        public uint WaitingTimeBetweenRetransmissions { get; set; } = 2000;

        /// <summary>
        /// Id that will be assigned to next sent message
        /// </summary>
        //internal int NextSentMessageId = 1;

        /// <summary>
        /// Stores `KnownSource` class instance for each Guid that ever appeared in any incoming message
        /// </summary>
        internal Dictionary<Guid, KnownSource> KnownSources = new Dictionary<Guid, KnownSource>();
        object knownSourcesLock = new object();

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

        public int MaxPacketSize { get; set; } = 65407;

        public ExtendedUdpClient(ILogger logger, Guid deviceId)
        {
            _logger = logger;
            DeviceId = deviceId;
        }

        public ExtendedUdpClient(IUdpClient udpClientInstance, ILogger logger, Guid deviceId) : this(logger, deviceId)
        {
            udpClient = udpClientInstance;
        }

        /// <summary>
        /// Begins listening for incoming messages on specified port
        /// </summary>
        /// <param name="port"></param>
        public void StartListening(int port)
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
            KnownSource foundSource = FindOrCreateSource(df.SourceNodeIdGuid);

            if (df.ReceiveAck)
            {
                TrackedMessage tm;
                AckStatus status = AckStatus.Failure; // failure by default

                lock (foundSource.trackedMessagesLock)
                {
                    foundSource.TrackedOutgoingMessages.TryGetValue(df.RetransmissionId, out tm);
                }

                if (tm == null)
                {
                    _logger.LogTrace($"Received ack for {df.RetransmissionId} but is not present in TrackedMessages (key: {df.SourceNodeIdGuid})");
                    return;
                }
                _logger.LogTrace($"Received ack for {df.RetransmissionId} and processing...");

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

                if (tm != null && tm.Callback != null)
                {
                    tm.Callback(status);
                }

                lock (foundSource.trackedMessagesLock)
                {
                    foundSource.TrackedOutgoingMessages.Remove(df.RetransmissionId);
                }

                // wake up retransmission thread in order to remove it
                lock (tm.ThreadLock)
                {
                    Monitor.Pulse(tm.ThreadLock);
                }
            }
            else
            {
                try
                {
                    foundSource.HandleNewMessage(senderIpEndPoint, df);
                }
                catch (Exception e)
                {
                    _logger.LogError($"Error while processing message. {e.Message}");
                }
            }

            udpClient.BeginReceive(new AsyncCallback(HandleIncomingMessages), null);
        }

        internal KnownSource FindOrCreateSource(Guid id)
        {
            KnownSource foundSource;
            lock (knownSourcesLock)
            {
                KnownSources.TryGetValue(id, out foundSource);

                if (foundSource == null)
                {
                    _logger.LogDebug($"Created new KnownSource for id {id}");
                    foundSource = new KnownSource(this, id, _logger);
                    KnownSources.Add(id, foundSource);
                }
            }

            return foundSource;
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
        public void SendMessageSequentially(IPEndPoint endPoint, byte[] payload, Guid source, Guid destination, Action<AckStatus> callback = null)
        {
            SendMessage(endPoint, payload, source, destination, true, callback);
        }

        public void SendMessageNonSequentially(IPEndPoint endPoint, byte[] payload, Guid source, Guid destination, Action<AckStatus> callback = null)
        {
            SendMessage(endPoint, payload, source, destination, false, callback);
        }

        private void SendMessage(IPEndPoint endPoint, byte[] payload, Guid source, Guid destination, bool sequentially, Action<AckStatus> callback = null)
        {
            KnownSource foundSource = FindOrCreateSource(destination);
            int retransmissionId = foundSource.NextIdForMessageToSend;

            var dataFrame = new DataFrame
            {
                Payload = payload,
                SourceNodeIdGuid = source,
                ExpectAcknowledge = true,
                RetransmissionId = retransmissionId,
                SendSequentially = sequentially
            };
            var bytes = dataFrame.PackToBytes();

            if (bytes.Length > MaxPacketSize)
            {
                throw new Exception($"Data packet of size {bytes.Length} exceeded maximal allowed size ({MaxPacketSize})");
            }

            udpClient.Send(bytes, bytes.Length, endPoint);

            _logger.LogTrace($"Sent message with retr. id: {dataFrame.RetransmissionId}");

            TrackedMessage tm = new TrackedMessage(bytes, endPoint, callback);
            foundSource.TrackedOutgoingMessages.Add(retransmissionId, tm);

            Thread thread = new Thread(() => RetransmissionThread(dataFrame.RetransmissionId, tm, foundSource));
            thread.IsBackground = true;
            thread.Start();

            foundSource.SetIdForMessageToSendToNextValue();
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
                SourceNodeIdGuid = source,
                ExpectAcknowledge = false,
                RetransmissionId = 0
            };
            var bytes = dataFrame.PackToBytes();

            if (bytes.Length > MaxPacketSize)
            {
                throw new Exception($"Data packet of size {bytes.Length} exceeded maximal allowed size ({MaxPacketSize})");
            }

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
                SourceNodeIdGuid = source,
                ExpectAcknowledge = false,
                RetransmissionId = messageId,
                ReceiveAck = true
            };
            var bytes = dataFrame.PackToBytes();

            if (bytes.Length > MaxPacketSize)
            {
                throw new Exception($"Data packet of size {bytes.Length} exceeded maximal allowed size ({MaxPacketSize})");
            }

            _logger.LogDebug($"Sent ReceiveAck for {messageId} from {source}");

            udpClient.Send(bytes, bytes.Length, endPoint);
        }

        internal void RetransmissionThread(int messageId, TrackedMessage tm, KnownSource ks)
        {
            _logger.LogDebug($"Began retransmissionThread of {messageId}");

            bool messageReceived = false;
            do
            {
                lock (tm.ThreadLock)
                {
                    Monitor.Wait(tm.ThreadLock, checked((int)WaitingTimeBetweenRetransmissions));
                }

                lock (ks.trackedMessagesLock)
                {
                    messageReceived = !ks.TrackedOutgoingMessages.ContainsKey(messageId);
                }

                if (messageReceived)
                {
                    _logger.LogTrace($"Stopping retransmission of {messageId}");
                    break;
                }

                _logger.LogTrace($"Retransmission of {messageId}");
                udpClient.Send(tm.Contents, tm.Contents.Length, tm.Endpoint);

            } while (!messageReceived);
        }

        /// <summary>
        /// Setting what `retransmissionId` message sent from `connectedDeviceGuid` should have
        /// in order to reach this device
        /// </summary>
        /// <param name="connectedDeviceGuid"></param>
        /// <param name="nextIdOfIncomingMessage"></param>
        public void ResetIncomingMessageCounterFor(Guid connectedDeviceGuid, int nextIdOfIncomingMessage)
        {
            var source = FindOrCreateSource(connectedDeviceGuid);

            source.ResetIncomingMessagesCounter(nextIdOfIncomingMessage);
        }

        public void ResetOutgoingMessageCounterFor(Guid connectedDeviceGuid, int nextIdOfOutgoingMessage)
        {
            var source = FindOrCreateSource(connectedDeviceGuid);

            source.ResetOutgoingMessagesCounter(nextIdOfOutgoingMessage);
        }

        public int GetIncomingMessageCounterFor(Guid connectedDeviceGuid)
        {
            var source = FindOrCreateSource(connectedDeviceGuid);

            return source.NextExpectedIncomingMessageId;
        }
    }
}
