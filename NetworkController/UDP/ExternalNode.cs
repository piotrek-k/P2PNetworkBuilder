using NetworkController.Encryption;
using NetworkController.Interfaces;
using System;
using ConnectionsManager.MessageFramework;
using Microsoft.Extensions.Logging;
using System.Net;
using NetworkController.Models;
using NetworkController.Interfaces.ForTesting;
using NetworkController.DataTransferStructures;
using System.Threading;
using System.Linq;
using NetworkController.Threads;
using ConnectionsManager.Debugging;
using NetworkController.DataTransferStructures.Other;
using NetworkController.UDP.MessageHandlers;
using NetworkController.Helpers;

namespace NetworkController.UDP
{
    public class ExternalNode : IExternalNode, IExternalNodeInternal
    {
        public IPEndPoint CurrentEndpoint
        {
            get
            {
                if (_currentEP == null)
                    return PublicEndpoint;
                return _currentEP;
            }
            private set { _currentEP = value; }
        }

        private IPEndPoint _currentEP = null;

        /// <summary>
        /// IP and Port of node as seen from node that shared information about him
        /// </summary>
        public IPEndPoint PublicEndpoint { get; set; }
        /// <summary>
        /// IP and Port of node in his private network
        /// </summary>
        public IPEndPoint ClaimedPrivateEndpoint { get; set; }

        public INetworkControllerInternal NetworkController { get; set; }
        public Guid Id { get; private set; }

        public AsymmetricEncryptionService Aes { get; set; }
        public SymmetricEncryptionService Ses { get; set; }

        /// <summary>
        /// Redirects incoming messages to proper functions handling them
        /// </summary>
        private readonly IncomingMessageCaller _imc;
        private readonly ILogger _logger;

        private event EventHandler<BytesReceivedEventArgs> bytesReceived;
        public event EventHandler<BytesReceivedEventArgs> BytesReceived
        {
            add
            {
                if (bytesReceived == null || !bytesReceived.GetInvocationList().Contains(value))
                {
                    bytesReceived += value;
                }
            }
            remove
            {
                bytesReceived -= value;
            }
        }

        private event EventHandler<ConnectionResetEventArgs> connectionReset;
        public event EventHandler<ConnectionResetEventArgs> ConnectionReset
        {
            add
            {
                if (connectionReset == null || !connectionReset.GetInvocationList().Contains(value))
                {
                    connectionReset += value;
                }
            }
            remove
            {
                connectionReset -= value;
            }
        }

        public virtual void OnBytesReceivedEvent(BytesReceivedEventArgs e)
        {
            EventHandler<BytesReceivedEventArgs> handler = bytesReceived;
            handler?.Invoke(this, e);
        }

        public virtual void OnConnectionResetEvent(ConnectionResetEventArgs e)
        {
            EventHandler<ConnectionResetEventArgs> handler = connectionReset;
            handler?.Invoke(this, e);
        }

        public TrackedConnection _tracker { get; set; }

        public enum ConnectionState : int
        {
            NotEstablished,
            Building,
            Failed,
            Ready,
            Shutdown,
        }

        private ConnectionState _currentState = ConnectionState.NotEstablished;
        public ConnectionState CurrentState
        {
            get { return _currentState; }
            set
            {
                bool changed = _currentState != value;
                _currentState = value;
                if (changed)
                    NetworkController.OnNetworkChangedEvent(null);
            }
        }

        /// <summary>
        /// If true, you can safely send messages
        /// </summary>
        public bool IsHandshakeCompleted { get; set; } = false;

        /// <summary>
        /// Thread for keeping connection active by periodically sending pings
        /// </summary>
        private KeepaliveThread _keepaliveThread;
        /// <summary>
        /// Cancellation token connected with keepalive thread
        /// </summary>
        private CancellationTokenSource _allChildThreadsCancellationToken;

        /// <summary>
        /// Class for sending messages and ensuring it was properly delivered
        /// </summary>
        private ITransmissionManager TransmissionManager
        {
            get { return _tansmissionManager; }
            set
            {
                if (!transmissionManagerWasPreset)
                {
                    _tansmissionManager = value;
                }
            }
        }
        private ITransmissionManager _tansmissionManager;
        private bool transmissionManagerWasPreset = false;

        /// <summary>
        /// Counter of messages ensuring their proper order
        /// </summary>
        public uint HighestReceivedSendingId { get; private set; }

        // Building states
        public bool AfterHolePunchingResponse_WaitingForPingResponse { get; set; } = false;

        public const string SAMPLE_ENCRYPTION_VERIFICATION_TEXT = "This is test";

        // Connection reset
        public DateTimeOffset? ConnectionResetExpirationTime = null;
        public const int CONNECTION_RESET_ALLOWANCE_WINDOW_SEC = 60 * 5;

        private ExternalNode(INetworkControllerInternal networkController, ILogger logger,
            TrackedConnection tracker, ITransmissionManager transmissionManager = null)
        {
            _tracker = tracker;
            NetworkController = networkController;
            _imc = new IncomingMessageCaller(logger);
            _logger = logger;
            _allChildThreadsCancellationToken = new CancellationTokenSource();
            _keepaliveThread = new KeepaliveThread(this, _allChildThreadsCancellationToken.Token, _logger);

            if (transmissionManager == null)
            {
                TransmissionManager = new TransmissionManager(networkController, this, logger);
            }
            else
            {
                TransmissionManager = transmissionManager;
                transmissionManagerWasPreset = true;
            }
        }

        public ExternalNode(Guid receivedId, INetworkControllerInternal networkController, ILogger logger,
            TrackedConnection tracker, ITransmissionManager transmissionManager = null)
            : this(networkController, logger, tracker, transmissionManager)
        {
            Id = receivedId;
        }

        public ExternalNode(IPEndPoint manualEndpoint, INetworkControllerInternal networkController, ILogger logger,
            TrackedConnection tracker, ITransmissionManager transmissionManager = null)
            : this(networkController, logger, tracker, transmissionManager)
        {
            Id = Guid.Empty;
            PublicEndpoint = manualEndpoint;
            CurrentEndpoint = manualEndpoint;
        }

        /// <summary>
        /// Send message and run callback if remote node confirms receiving it
        /// </summary>
        /// <param name="type"></param>
        /// <param name="payloadOfDataFrame"></param>
        /// <param name="callback"></param>
        public void SendBytes(int type, byte[] payloadOfDataFrame, Action<AckStatus> callback = null)
        {
            SendBytes(type, payloadOfDataFrame, CurrentEndpoint, true, 0, callback);
        }

        /// <summary>
        /// Send message as connectionless UDP datagram
        /// </summary>
        /// <param name="type"></param>
        /// <param name="payloadOfDataFrame"></param>
        public void SendAndForget(int type, byte[] payloadOfDataFrame)
        {
            SendBytes(type, payloadOfDataFrame, CurrentEndpoint, false, 0);
        }

        /// <summary>
        /// Send message to custom IPEndpoint associated with current node
        /// </summary>
        /// <param name="type"></param>
        /// <param name="payloadOfDataFrame"></param>
        /// <param name="endpoint"></param>
        /// <param name="ensureDelivered"></param>
        public void SendBytes(int type, byte[] payloadOfDataFrame, IPEndPoint endpoint, bool ensureDelivered)
        {
            SendBytes(type, payloadOfDataFrame, endpoint, ensureDelivered, 0);
        }

        /// <summary>
        /// Send message confirming receiving other message
        /// </summary>
        /// <param name="retransmissionId">id of message that delvery is being confirmed</param>
        public void SendReceiveAcknowledge(uint retransmissionId, AckStatus status)
        {
            SendBytes((int)MessageType.ReceiveAcknowledge, new ReceiveAcknowledge()
            {
                Status = (int)status
            }.PackToBytes(), CurrentEndpoint, false, retransmissionId);
        }

        private void SendBytes(int type, byte[] payloadOfDataFrame, IPEndPoint endpoint, bool ensureDelivered,
            uint retransmissionId, Action<AckStatus> callback = null)
        {
            byte[] generatedSesIV = null;
            byte[] encryptedPaylaod = null;
            if (payloadOfDataFrame != null)
            {
                if (MessageTypeGroups.DoesntRequireEncryption(type))
                {
                    encryptedPaylaod = payloadOfDataFrame;
                    _logger.LogTrace(new EventId((int)LoggerEventIds.DataUnencrypted), "Data sent unencrypted");
                }
                else if (type == (int)MessageType.PrivateKey)
                {
                    encryptedPaylaod = Aes.Encrypt(payloadOfDataFrame);
                }
                else if (Ses != null)
                {
                    generatedSesIV = Ses.GetIV();
                    encryptedPaylaod = Ses.Encrypt(payloadOfDataFrame, generatedSesIV);
                }
                else
                {
                    _logger.LogCritical("Message didn't sent. Didn't know what to do at encryption step.");
                    return;
                }
            }

            if (CurrentState != ConnectionState.Ready &&
                !MessageTypeGroups.CanBeSentWhenConnectionIsNotYetBuilt(type))
            {
                _logger.LogWarning($"Sending message before handshaking finished ({Enum.GetName(typeof(MessageType), type)})");
            }

            var data = new DataFrame
            {
                MessageType = type,
                Payload = encryptedPaylaod,
                PayloadSize = payloadOfDataFrame != null ? payloadOfDataFrame.Length : 0,
                SourceNodeIdGuid = NetworkController.DeviceId,
                ExpectAcknowledge = ensureDelivered,
                RetransmissionId = retransmissionId,
                IV = generatedSesIV
            };

            _tracker.AddNewEvent(new ConnectionEvents(PossibleEvents.OutgoingMessage, GetMessageName(type) + " transm. id: " + data.RetransmissionId));
            if (!MessageTypeGroups.IsKeepaliveNoLogicRelatedMessage(data.MessageType))
            {
                _logger.LogTrace($"{Id} \t Outgoing: {GetMessageName(data.MessageType) + " transm. id: " + data.RetransmissionId}, payload {encryptedPaylaod?.Length}B ({payloadOfDataFrame?.Length}B unenc)");
            }

            if (ensureDelivered)
            {
                TransmissionManager.SendFrameEnsureDelivered(data, endpoint, callback);
            }
            else
            {
                TransmissionManager.SendFrameAndForget(data, endpoint);
            }
        }

        /// <summary>
        /// Tries to get name of message based on its id.
        /// Names are based on MessagetTypes registred in NetworkController
        /// </summary>
        /// <param name="msgId"></param>
        /// <returns></returns>
        private string GetMessageName(int msgId)
        {
            foreach (var t in NetworkController.GetMessageTypes())
            {
                try
                {
                    var name = Enum.GetName(t, msgId);
                    if (name != null)
                        return name;
                }
                catch (Exception)
                {
                }
            }

            return null;
        }

        /// <summary>
        /// Sends PublicKey to external node to begin connection
        /// </summary>
        public void InitializeConnection(uint? proposedRetransmissionId = null)
        {
            Aes = new AsymmetricEncryptionService();
            var payload = new ConnectionInitPublicKey()
            {
                RsaParams = Aes.PublicKey,
                ProposedStartingRetransmissionId = proposedRetransmissionId
            }.PackToBytes();

            CurrentState = ConnectionState.Building;

            SendBytes((int)MessageType.PublicKey, payload);
        }

        public void HandleIncomingBytes(DataFrame dataFrame)
        {
            try
            {
                _tracker.AddNewEvent(new ConnectionEvents(PossibleEvents.IncomingMessage,
                    GetMessageName(dataFrame.MessageType) + " transm. id: " + dataFrame.RetransmissionId));
                if (!MessageTypeGroups.IsKeepaliveNoLogicRelatedMessage(dataFrame.MessageType))
                {
                    _logger.LogDebug($"{Id} \t Incoming: {GetMessageName(dataFrame.MessageType) + " transm. id: " + dataFrame.RetransmissionId}");
                }

                if (CurrentState == ConnectionState.Shutdown)
                {
                    if (dataFrame.MessageType == (int)MessageType.Restart)
                    {
                        CurrentState = ConnectionState.Ready;
                        // TODO: change starting value to dynamically generated (like on connection restoring)
                        TransmissionManager.SetupIfNotWorking(1);
                    }
                    else
                    {
                        _logger.LogWarning("Received message but ignored it as connection is shut down.");
                        return;
                    }
                }

                RestoreIfFailed();

                /**
                 * RESETTING CONNECTION
                 */

                if (dataFrame.MessageType == (int)MessageType.PublicKey && ConnectionResetExpirationTime != null &&
                    ConnectionResetExpirationTime > DateTimeOffset.UtcNow)
                {
                    // reset request was sent by this node and exernal node sent PublicKey as response
                    var pubKeyObj = ConnectionInitPublicKey.Unpack(dataFrame.Payload);

                    ConnectionResetExpirationTime = null;
                    if (pubKeyObj.ProposedStartingRetransmissionId == null)
                    {
                        throw new Exception("Resetting connection: Public key doesn't contain proposed retransmission id");
                    }
                    else
                    {
                        PerformConnectionReset(false, pubKeyObj.ProposedStartingRetransmissionId.Value);

                        SendReceiveAcknowledge(dataFrame.RetransmissionId, AckStatus.Success);

                        HighestReceivedSendingId = dataFrame.RetransmissionId - 1;
                    }
                }

                if (dataFrame.MessageType == (int)MessageType.ResetRequest)
                {
                    // other Node lost keys and wants to reset connection

                    if (dataFrame.ExpectAcknowledge)
                    {
                        SendReceiveAcknowledge(dataFrame.RetransmissionId, AckStatus.Success);
                    }

                    if (NetworkController.ConnectionResetRule(this))
                    {
                        _logger.LogInformation("Connection reset request accepted");
                        uint newRetransmissionId = GenerateNewSafeRetransmissionId(dataFrame);

                        PerformConnectionReset(true, newRetransmissionId);

                        return;
                    }
                    else
                    {
                        _logger.LogInformation("Connection reset revoked");

                        return;
                    }
                }

                /**
                 * PREVENTING UNFORSEEN BEHAVIOUR
                 */

                if (dataFrame.MessageType == (int)MessageType.PublicKey && Ses != null)
                {
                    _logger.LogTrace("Omitting retransmitted PublicKey");
                    return;
                }

                if (MessageTypeGroups.IsHandshakeRelatedAndShouldntBeDoneMoreThanOnce(dataFrame.MessageType) &&
                    IsHandshakeCompleted)
                {
                    throw new Exception("Handshake related message when handshake is completed");
                }

                if (!MessageTypeGroups.DoesntRequireEncryption(dataFrame.MessageType) &&
                    Ses == null && Aes == null)
                {
                    throw new Exception("Received message that cannot be handled at the moment");
                }

                /**
                 * DECRYPTING
                 */

                byte[] decryptedPayload = null;
                if (dataFrame.Payload != null)
                {
                    if (MessageTypeGroups.DoesntRequireEncryption(dataFrame.MessageType))
                    {
                        decryptedPayload = dataFrame.Payload;
                    }
                    else
                    {
                        if (dataFrame.MessageType == (int)MessageType.PrivateKey)
                        {
                            decryptedPayload = Aes.Decrypt(dataFrame.Payload);
                        }
                        else if (Ses != null)
                        {
                            decryptedPayload = Ses.Decrypt(dataFrame.Payload, dataFrame.IV);
                        }
                        else
                        {
                            decryptedPayload = dataFrame.Payload;
                        }
                    }
                }

                if (decryptedPayload != null && decryptedPayload.Length != dataFrame.PayloadSize)
                {
                    // encryption algorithms might leave zero paddings which have to be removed
                    _logger.LogTrace("Payload size correction");
                    decryptedPayload = decryptedPayload.Take(dataFrame.PayloadSize).ToArray();
                }

                /**
                 * RESETTING COUNTER
                 */

                if (dataFrame.MessageType == (int)MessageType.ConnectionRestoreRequest)
                {
                    var data = ConnectionRestoreRequest.Unpack(dataFrame.Payload);

                    if (!data.SampleDataForEncryptionVerification.Equals(SAMPLE_ENCRYPTION_VERIFICATION_TEXT))
                    {
                        throw new Exception("Failed to properly decrypt message");
                    }
                    else
                    {
                        SendReceiveAcknowledge(dataFrame.RetransmissionId, AckStatus.Success);

                        uint newRetransmissionId = GenerateNewSafeRetransmissionId(dataFrame);

                        ResetMessageCounter(newRetransmissionId, newRetransmissionId);

                        SendBytes((int)MessageType.ConnectionRestoreResponse, new ConnectionRestoreResponse()
                        {
                            ProposedStartingRetransmissionId = newRetransmissionId
                        }.PackToBytes());

                        IsHandshakeCompleted = false;

                        return;
                    }
                }

                if (dataFrame.MessageType == (int)MessageType.ConnectionRestoreResponse)
                {
                    var data = ConnectionRestoreResponse.Unpack(dataFrame.Payload);

                    // +1 because other side already sent one message after counter reset
                    ResetMessageCounter(data.ProposedStartingRetransmissionId + 1, dataFrame.RetransmissionId);

                    SendReceiveAcknowledge(dataFrame.RetransmissionId, AckStatus.Success);

                    SendBytes((int)MessageType.AdditionalInfoRequest, null, (air_status) =>
                    {
                        var ai = HandshakeController.GenerateAdditionalInfo(this);
                        SendBytes((int)MessageType.AdditionalInfo, ai.PackToBytes());
                    });

                    //return;
                }

                if (dataFrame.MessageType == (int)MessageType.Shutdown)
                {
                    _currentState = ConnectionState.Shutdown;
                    TransmissionManager.GentleShutdown();
                }

                if (dataFrame.MessageType == (int)MessageType.ReceiveAcknowledge)
                {
                    TransmissionManager.ReportReceivingDataArrivalAcknowledge(dataFrame, ReceiveAcknowledge.Unpack(decryptedPayload));
                    return;
                }

                // checking dataFrame.RetransmissionId != 0 because messages that are not meant to be retransmitted leave it to 0

                if ((HighestReceivedSendingId + 1 != dataFrame.RetransmissionId
                    || (HighestReceivedSendingId == uint.MaxValue && dataFrame.RetransmissionId != 1))
                    && dataFrame.RetransmissionId != 0)
                {
                    _logger.LogDebug($"Message {dataFrame.RetransmissionId} omitted as it has incorrect transmission id" +
                        $" (got {dataFrame.RetransmissionId} but should be " +
                        $"{(HighestReceivedSendingId != uint.MaxValue ? HighestReceivedSendingId + 1 : 1)})");
                    return;
                }


                if (!_imc.Call(this, dataFrame.MessageType, decryptedPayload))
                {
                    // Message not found. Pass it further.

                    OnBytesReceivedEvent(new BytesReceivedEventArgs
                    {
                        Sender = this,
                        MessageType = (int)dataFrame.MessageType,
                        Payload = decryptedPayload
                    });
                }

                if (dataFrame.RetransmissionId != 0)
                {
                    HighestReceivedSendingId = dataFrame.RetransmissionId;
                }

                if (dataFrame.ExpectAcknowledge)
                {
                    SendReceiveAcknowledge(dataFrame.RetransmissionId, AckStatus.Success);
                }
            }
            catch (Exception e)
            {
                _logger.LogError($"Error while processing message number {dataFrame.RetransmissionId} of type" +
                    $" {GetMessageName((int)dataFrame.MessageType)}({(int)dataFrame.MessageType}). {e.Message}");

                if (dataFrame.RetransmissionId != 0)
                {
                    HighestReceivedSendingId = dataFrame.RetransmissionId;
                }

                if (dataFrame.ExpectAcknowledge)
                {
                    SendReceiveAcknowledge(dataFrame.RetransmissionId, AckStatus.Failure);
                }

                throw;
            }
        }

        /// <summary>
        /// Generate retransmissionId that will be far from actual values, so retransmitted messages won't
        /// interfere app message flow
        /// </summary>
        /// <param name="dataFrame"></param>
        /// <returns></returns>
        private uint GenerateNewSafeRetransmissionId(DataFrame dataFrame)
        {
            //TODO: handle int overflow
            return Math.Max(dataFrame.RetransmissionId + 10, HighestReceivedSendingId + 10);
        }

        private void PerformConnectionReset(bool sendPublicKey, uint newSendingId)
        {
            _logger.LogInformation("Resetting connection");

            Aes = null;
            Ses = null;

            if (sendPublicKey)
            {
                ResetMessageCounter(newSendingId, newSendingId);
            }
            else
            {
                // if we are here, that means node processes PublicKey
                // and it's reset response. In that case, newSendingId
                // should be bigger to take sent message into account
                ResetMessageCounter(newSendingId + 1, newSendingId);
            }

            IsHandshakeCompleted = false;

            if (sendPublicKey)
            {
                // begin handshake
                InitializeConnection(newSendingId);
            }

            OnConnectionResetEvent(new ConnectionResetEventArgs
            {
                RelatedNode = this
            });
        }

        /// <summary>
        /// Asks Node to perform handshaking again. If Node agrees, encryption keys will be cleared.
        /// </summary>
        public void RestartConnection()
        {
            SendBytes((int)MessageType.ResetRequest, new ResetRequest().PackToBytes());
            ConnectionResetExpirationTime = DateTimeOffset.UtcNow.AddSeconds(CONNECTION_RESET_ALLOWANCE_WINDOW_SEC);
        }

        /// <summary>
        /// If connection is failed, set it to Ready and restore all necessary threads
        /// that might be suspended during failure
        /// </summary>
        private void RestoreIfFailed()
        {
            if (CurrentState == ConnectionState.Failed)
            {
                CurrentState = ConnectionState.Ready;
                _keepaliveThread.BeginKeepaliveThread();
                _logger.LogInformation($"{Id} connection restored");
            }
        }

        public void SendPingSeries()
        {
            _keepaliveThread.InitialConnectionPings();
        }

        public void ReportIncomingPingResponse()
        {
            _keepaliveThread.InformAboutResponse();

            RestoreIfFailed();
        }

        public void ReportThatConnectionIsSetUp()
        {
            _keepaliveThread.BeginKeepaliveThread();
        }

        public void ReportConnectionFailure()
        {
            if (CurrentState != ConnectionState.Failed)
                _logger.LogError($"Connection lost with {Id}");
            CurrentState = ConnectionState.Failed;
        }

        public void FillCurrentEndpoint(IPEndPoint proposedEndpoint)
        {
            if (CurrentEndpoint != null)
            {
                _logger.LogWarning($"Trying to fill currentEndpoint with {proposedEndpoint} when it's already filled with {CurrentEndpoint}");
            }

            CurrentEndpoint = proposedEndpoint;
        }

        public void SetId(Guid newId)
        {
            if (Id != null && Id != Guid.Empty)
            {
                _logger.LogError("Id already set");
                return;
            }

            if (NetworkController.GetNodes_Internal().Any(x => x.Id == newId))
            {
                _logger.LogError("Id already known in network");
                return;
            }

            Id = newId;
        }

        public void ResetMessageCounter(uint newSendingId, uint newHighestReceivedId)
        {
            _logger.LogDebug("Counter reset");
            TransmissionManager.Destroy();
            if (!transmissionManagerWasPreset)
            {
                TransmissionManager = new TransmissionManager(NetworkController, this, _logger);
            }
            TransmissionManager.SetupIfNotWorking(newSendingId, this);
            HighestReceivedSendingId = newHighestReceivedId;
        }

        public void RestoreSecurityKeys(byte[] key, Action actionOnFailure = null)
        {
            Ses = new SymmetricEncryptionService(key);
            CurrentState = ConnectionState.Building;

            SendBytes((int)MessageType.ConnectionRestoreRequest, new ConnectionRestoreRequest()
            {
                SampleDataForEncryptionVerification = SAMPLE_ENCRYPTION_VERIFICATION_TEXT
            }.PackToBytes(), (crr_status) =>
            {
                if (crr_status == AckStatus.Failure && actionOnFailure != null)
                {
                    CurrentState = ConnectionState.Failed;
                    actionOnFailure();
                }
            });
        }

        public byte[] GetSecurityKeys()
        {
            if (Ses != null)
            {
                return Ses.Aes.Key;
            }
            return null;
        }

        /// <summary>
        /// May be necessary to destroy object
        /// </summary>
        public void ClearEvents()
        {
            bytesReceived = null;
            connectionReset = null;
        }
    }
}