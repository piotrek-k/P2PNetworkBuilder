﻿using NetworkController.Encryption;
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
using NetworkController.UDP.MessageHandlers;
using NetworkController.Helpers;
using TransmissionComponent;
using TransmissionComponent.Structures.Other;

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
        private ITransmissionHandler _transmissionHandler;

        /// <summary>
        /// Counter of messages ensuring their proper order
        /// </summary>
        public uint HighestReceivedSendingId { get; private set; }

        // Building states
        /// <summary>
        /// If true, nodes is waiting for ping in order to begin connection
        /// </summary>
        public bool AfterHolePunchingResponse_WaitingForPingResponse { get; set; } = false;

        public const string SAMPLE_ENCRYPTION_VERIFICATION_TEXT = "This is test";

        // Connection reset
        public DateTimeOffset? ConnectionResetExpirationTime = null;
        public const int CONNECTION_RESET_ALLOWANCE_WINDOW_SEC = 60 * 5;

        private ExternalNode(INetworkControllerInternal networkController, ILogger logger,
            TrackedConnection tracker, ITransmissionHandler transmissionHandler)
        {
            _tracker = tracker;
            NetworkController = networkController;
            _imc = new IncomingMessageCaller(logger);
            _logger = logger;
            _allChildThreadsCancellationToken = new CancellationTokenSource();
            _keepaliveThread = new KeepaliveThread(this, _allChildThreadsCancellationToken.Token, _logger);

            _transmissionHandler = transmissionHandler;
        }

        public ExternalNode(Guid receivedId, INetworkControllerInternal networkController, ILogger logger,
            TrackedConnection tracker, ITransmissionHandler transmissionHandler)
            : this(networkController, logger, tracker, transmissionHandler)
        {
            Id = receivedId;
        }

        public ExternalNode(IPEndPoint manualEndpoint, INetworkControllerInternal networkController, ILogger logger,
            TrackedConnection tracker, ITransmissionHandler transmissionHandler)
            : this(networkController, logger, tracker, transmissionHandler)
        {
            Id = Guid.Empty;
            PublicEndpoint = manualEndpoint;
            CurrentEndpoint = manualEndpoint;
        }

        public void SendMessageSequentially(int messageType, byte[] payload, Action<AckStatus> callback = null)
        {
            var builtFrame = EncryptAndPrepareForSending(messageType, payload);

            _transmissionHandler.SendMessageSequentially(CurrentEndpoint, builtFrame, NetworkController.DeviceId, callback);
        }

        public void SendMessageNonSequentially(int messageType, byte[] payload, Action<AckStatus> callback = null)
        {
            var builtFrame = EncryptAndPrepareForSending(messageType, payload);

            _transmissionHandler.SendMessageNonSequentially(CurrentEndpoint, builtFrame, NetworkController.DeviceId, callback);
        }
        public void SendMessageNonSequentially(int messageType, byte[] payload, IPEndPoint customEndpoint, Action<AckStatus> callback = null)
        {
            var builtFrame = EncryptAndPrepareForSending(messageType, payload);

            _transmissionHandler.SendMessageNonSequentially(customEndpoint, builtFrame, NetworkController.DeviceId, callback);
        }

        public void SendAndForget(int type, byte[] payloadOfDataFrame, IPEndPoint customEndpoint = null)
        {
            var builtFrame = EncryptAndPrepareForSending(type, payloadOfDataFrame);

            if (customEndpoint != null)
                _transmissionHandler.SendMessageNoTracking(customEndpoint, builtFrame, NetworkController.DeviceId);
            else
                _transmissionHandler.SendMessageNoTracking(CurrentEndpoint, builtFrame, NetworkController.DeviceId);
        }

        byte[] EncryptAndPrepareForSending(int messageType, byte[] payload)
        {
            byte[] generatedSesIV = null;
            byte[] encryptedPaylaod = null;
            if (payload != null)
            {
                if (MessageTypeGroups.DoesntRequireEncryption(messageType))
                {
                    encryptedPaylaod = payload;
                    _logger.LogTrace(new EventId((int)LoggerEventIds.DataUnencrypted), "Data sent unencrypted");
                }
                else if (messageType == (int)MessageType.PrivateKey)
                {
                    encryptedPaylaod = Aes.Encrypt(payload);
                }
                else if (Ses != null)
                {
                    generatedSesIV = Ses.GetIV();
                    encryptedPaylaod = Ses.Encrypt(payload, generatedSesIV);
                }
                else
                {
                    throw new Exception("Message didn't sent. Didn't know what to do at encryption step.");
                }
            }

            if (CurrentState != ConnectionState.Ready &&
                !MessageTypeGroups.CanBeSentWhenConnectionIsNotYetBuilt(messageType))
            {
                _logger.LogWarning($"Sending message before handshaking finished ({Enum.GetName(typeof(MessageType), messageType)})");
            }

            if (!MessageTypeGroups.IsKeepaliveNoLogicRelatedMessage(messageType))
            {
                _logger.LogTrace($"{Id} \t Outgoing: {GetMessageName(messageType)}, payload {payload?.Length}B");
            }

            return new NC_DataFrame
            {
                IV = generatedSesIV,
                MessageType = messageType,
                PayloadSize = payload != null ? payload.Length : 0,
                Payload = encryptedPaylaod
            }.PackToBytes();
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

            SendMessageSequentially((int)MessageType.PublicKey, payload);
        }

        public AckStatus HandleIncomingBytes(NC_DataFrame ncDataFrame)
        {
            //if (CurrentState == ConnectionState.Shutdown)
            //{
            //    if (dataFrame.MessageType == (int)MessageType.Restart)
            //    {
            //        CurrentState = ConnectionState.Ready;
            //        // TODO: change starting value to dynamically generated (like on connection restoring)
            //        TransmissionManager.SetupIfNotWorking(1);
            //    }
            //    else
            //    {
            //        _logger.LogWarning("Received message but ignored it as connection is shut down.");
            //        return;
            //    }
            //}

            RestoreIfFailed();

            /**
             * RESETTING CONNECTION
             */

            //if (dataFrame.MessageType == (int)MessageType.PublicKey && ConnectionResetExpirationTime != null &&
            //    ConnectionResetExpirationTime > DateTimeOffset.UtcNow)
            //{
            //    // reset request was sent by this node and exernal node sent PublicKey as response
            //    var pubKeyObj = ConnectionInitPublicKey.Unpack(dataFrame.Payload);

            //    ConnectionResetExpirationTime = null;
            //    if (pubKeyObj.ProposedStartingRetransmissionId == null)
            //    {
            //        throw new Exception("Resetting connection: Public key doesn't contain proposed retransmission id");
            //    }
            //    else
            //    {
            //        PerformConnectionReset(false, pubKeyObj.ProposedStartingRetransmissionId.Value);

            //        SendReceiveAcknowledge(dataFrame.RetransmissionId, AckStatus.Success);

            //        HighestReceivedSendingId = dataFrame.RetransmissionId - 1;
            //    }
            //}

            //if (dataFrame.MessageType == (int)MessageType.ResetRequest)
            //{
            //    // other Node lost keys and wants to reset connection

            //    if (dataFrame.ExpectAcknowledge)
            //    {
            //        SendReceiveAcknowledge(dataFrame.RetransmissionId, AckStatus.Success);
            //    }

            //    if (NetworkController.ConnectionResetRule(this))
            //    {
            //        _logger.LogInformation("Connection reset request accepted");
            //        uint newRetransmissionId = GenerateNewSafeRetransmissionId(dataFrame);

            //        PerformConnectionReset(true, newRetransmissionId);

            //        return;
            //    }
            //    else
            //    {
            //        _logger.LogInformation("Connection reset revoked");

            //        return;
            //    }
            //}

            /**
             * PREVENTING UNFORSEEN BEHAVIOUR
             */

            if (ncDataFrame.MessageType == (int)MessageType.PublicKey && Ses != null)
            {
                _logger.LogTrace("Unexpected PublicKey");
                return AckStatus.Failure;
            }

            if (MessageTypeGroups.IsHandshakeRelatedAndShouldntBeDoneMoreThanOnce(ncDataFrame.MessageType) &&
                IsHandshakeCompleted)
            {
                throw new Exception("Handshake related message when handshake is completed");
            }

            if (!MessageTypeGroups.DoesntRequireEncryption(ncDataFrame.MessageType) &&
                Ses == null && Aes == null)
            {
                throw new Exception("Received message that cannot be handled at the moment");
            }

            /**
             * DECRYPTING
             */

            byte[] decryptedPayload = null;
            if (ncDataFrame.Payload != null)
            {
                if (MessageTypeGroups.DoesntRequireEncryption(ncDataFrame.MessageType))
                {
                    decryptedPayload = ncDataFrame.Payload;
                }
                else
                {
                    if (ncDataFrame.MessageType == (int)MessageType.PrivateKey)
                    {
                        decryptedPayload = Aes.Decrypt(ncDataFrame.Payload);
                    }
                    else if (Ses != null)
                    {
                        decryptedPayload = Ses.Decrypt(ncDataFrame.Payload, ncDataFrame.IV);
                    }
                    else
                    {
                        decryptedPayload = ncDataFrame.Payload;
                    }
                }
            }

            if (decryptedPayload != null && decryptedPayload.Length != ncDataFrame.PayloadSize)
            {
                // encryption algorithms might leave zero paddings which have to be removed
                _logger.LogTrace("Payload size correction");
                decryptedPayload = decryptedPayload.Take(ncDataFrame.PayloadSize).ToArray();
            }

            /**
             * RESETTING COUNTER
             */

            //if (dataFrame.MessageType == (int)MessageType.ConnectionRestoreRequest)
            //{
            //    var data = ConnectionRestoreRequest.Unpack(dataFrame.Payload);

            //    if (!data.SampleDataForEncryptionVerification.Equals(SAMPLE_ENCRYPTION_VERIFICATION_TEXT))
            //    {
            //        throw new Exception("Failed to properly decrypt message");
            //    }
            //    else
            //    {
            //        SendReceiveAcknowledge(dataFrame.RetransmissionId, AckStatus.Success);

            //        uint newRetransmissionId = GenerateNewSafeRetransmissionId(dataFrame);

            //        ResetMessageCounter(newRetransmissionId, newRetransmissionId);

            //        SendBytes((int)MessageType.ConnectionRestoreResponse, new ConnectionRestoreResponse()
            //        {
            //            ProposedStartingRetransmissionId = newRetransmissionId
            //        }.PackToBytes());

            //        IsHandshakeCompleted = false;

            //        return;
            //    }
            //}

            //if (dataFrame.MessageType == (int)MessageType.ConnectionRestoreResponse)
            //{
            //    var data = ConnectionRestoreResponse.Unpack(dataFrame.Payload);

            //    // +1 because other side already sent one message after counter reset
            //    ResetMessageCounter(data.ProposedStartingRetransmissionId + 1, dataFrame.RetransmissionId);

            //    SendReceiveAcknowledge(dataFrame.RetransmissionId, AckStatus.Success);

            //    SendBytes((int)MessageType.AdditionalInfoRequest, null, (air_status) =>
            //    {
            //        var ai = HandshakeController.GenerateAdditionalInfo(this);
            //        SendBytes((int)MessageType.AdditionalInfo, ai.PackToBytes());
            //    });

            //    return;
            //}

            //if (dataFrame.MessageType == (int)MessageType.Shutdown)
            //{
            //    _currentState = ConnectionState.Shutdown;
            //    TransmissionManager.GentleShutdown();
            //}

            if (!_imc.Call(this, ncDataFrame.MessageType, decryptedPayload))
            {
                // Message not found. Pass it further.

                OnBytesReceivedEvent(new BytesReceivedEventArgs
                {
                    Sender = this,
                    MessageType = (int)ncDataFrame.MessageType,
                    Payload = decryptedPayload
                });
            }

            return AckStatus.Success;
        }

        //private void PerformConnectionReset(bool sendPublicKey, uint newSendingId)
        //{
        //    _logger.LogInformation("Resetting connection");

        //    Aes = null;
        //    Ses = null;

        //    if (sendPublicKey)
        //    {
        //        ResetMessageCounter(newSendingId, newSendingId);
        //    }
        //    else
        //    {
        //        // if we are here, that means node processes PublicKey
        //        // and it's reset response. In that case, newSendingId
        //        // should be bigger to take sent message into account
        //        ResetMessageCounter(newSendingId + 1, newSendingId);
        //    }

        //    IsHandshakeCompleted = false;

        //    if (sendPublicKey)
        //    {
        //        // begin handshake
        //        InitializeConnection(newSendingId);
        //    }

        //    OnConnectionResetEvent(new ConnectionResetEventArgs
        //    {
        //        RelatedNode = this
        //    });
        //}

        /// <summary>
        /// Asks Node to perform handshaking again. If Node agrees, encryption keys will be cleared.
        /// </summary>
        public void RestartConnection()
        {
            SendMessageSequentially((int)MessageType.ResetRequest, new ResetRequest().PackToBytes());
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

        public void RestoreSecurityKeys(byte[] key, Action actionOnFailure = null)
        {
            Ses = new SymmetricEncryptionService(key);
            CurrentState = ConnectionState.Building;

            SendMessageSequentially((int)MessageType.ConnectionRestoreRequest, new ConnectionRestoreRequest()
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