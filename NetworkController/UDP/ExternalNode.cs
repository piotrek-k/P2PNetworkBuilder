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
        private ITransmissionManager _transmissionManager;
        /// <summary>
        /// Counter of messages ensuring their proper order
        /// </summary>
        private ulong _highestReceivedSendingId;

        // Building states
        public bool AfterHolePunchingResponse_WaitingForPingResponse { get; set; } = false;

        private ExternalNode(INetworkControllerInternal networkController, ILogger logger,
            TrackedConnection tracker, ITransmissionManager transmissionManager = null)
        {
            _tracker = tracker;
            NetworkController = networkController;
            _imc = new IncomingMessageCaller(logger);
            _logger = logger;
            _allChildThreadsCancellationToken = new CancellationTokenSource();
            _keepaliveThread = new KeepaliveThread(this, _allChildThreadsCancellationToken.Token);

            if (transmissionManager == null)
            {
                _transmissionManager = new TransmissionManager(networkController, this, logger);
            }
            else
            {
                _transmissionManager = transmissionManager;
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
        public void SendBytes(int type, byte[] payloadOfDataFrame, Action callback = null)
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
        public void SendReceiveAcknowledge(uint retransmissionId)
        {
            SendBytes((int)MessageType.ReceiveAcknowledge, null, CurrentEndpoint, false, retransmissionId);
        }

        private void SendBytes(int type, byte[] payloadOfDataFrame, IPEndPoint endpoint, bool ensureDelivered,
            uint retransmissionId, Action callback = null)
        {
            byte[] encryptedPaylaod = null;
            if (payloadOfDataFrame != null)
            {
                if (type == (int)MessageType.PrivateKey)
                {
                    encryptedPaylaod = Aes.Encrypt(payloadOfDataFrame);
                }
                else if (Ses != null)
                {
                    encryptedPaylaod = Ses.Encrypt(payloadOfDataFrame);
                }
                else
                {
                    encryptedPaylaod = payloadOfDataFrame;
                    _logger.LogWarning(new EventId((int)LoggerEventIds.DataUnencrypted), "Data sent unencrypted");
                }
            }

            var data = new DataFrame
            {
                MessageType = type,
                Payload = encryptedPaylaod,
                SourceNodeId = NetworkController.DeviceId,
                ExpectAcknowledge = ensureDelivered,
                RetransmissionId = retransmissionId
            };

            if (ensureDelivered)
            {
                _transmissionManager.SendFrameEnsureDelivered(data, endpoint, callback);
            }
            else
            {
                _transmissionManager.SendFrameAndForget(data, endpoint);
            }

            _tracker.AddNewEvent(new ConnectionEvents(PossibleEvents.OutgoingMessage, GetMessageName(type) + " transm. id: " + data.RetransmissionId));
            //_logger.LogDebug($"{Id} \t Outgoing: {GetMessageName(data.MessageType) + " transm. id: " + data.RetransmissionId}");
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
        public void InitializeConnection()
        {
            Aes = new AsymmetricEncryptionService();
            var payload = new ConnectionInitPublicKey()
            {
                RsaParams = Aes.PublicKey
            }.PackToBytes();

            CurrentState = ConnectionState.Building;

            SendBytes((int)MessageType.PublicKey, payload);
        }

        public void HandleIncomingBytes(DataFrame dataFrame)
        {
            _tracker.AddNewEvent(new ConnectionEvents(PossibleEvents.IncomingMessage,
                GetMessageName(dataFrame.MessageType) + " transm. id: " + dataFrame.RetransmissionId));
            if (dataFrame.MessageType != (int)MessageType.Ping && dataFrame.MessageType != (int)MessageType.PingResponse)
            {
                _logger.LogDebug($"{Id} \t Incoming: {GetMessageName(dataFrame.MessageType) + " transm. id: " + dataFrame.RetransmissionId}");
            }

            if (CurrentState == ConnectionState.Shutdown)
            {
                if (dataFrame.MessageType == (int)MessageType.Restart)
                {
                    CurrentState = ConnectionState.Ready;
                    _transmissionManager.SetupIfNotWorking();
                }
                else
                {
                    _logger.LogWarning("Received message but ignored it as connection is shut down.");
                    return;
                }
            }

            RestoreIfFailed();

            if (dataFrame.MessageType == (int)MessageType.PublicKey &&
                Ses != null)
            {
                // Connection reset request
                if (NetworkController.ConnectionResetRule())
                {
                    _logger.LogInformation("Connection reset request accepted");
                    Aes = null;
                    Ses = null;
                    _transmissionManager = new TransmissionManager(NetworkController, this, _logger);
                    _highestReceivedSendingId = 0;

                    OnConnectionResetEvent(new ConnectionResetEventArgs
                    {
                        RelatedNode = this
                    });
                }
                else
                {
                    _logger.LogInformation("Connection reset revoked");
                }
            }

            if (dataFrame.MessageType != (int)MessageType.PublicKey &&
                dataFrame.MessageType != (int)MessageType.Ping &&
                dataFrame.MessageType != (int)MessageType.PingResponse &&
                dataFrame.MessageType != (int)MessageType.ReceiveAcknowledge &&
                Ses == null && Aes == null)
            {
                _logger.LogError("Received message that cannot be handled at the moment");
                return;
            }

            byte[] decryptedPayload = null;
            if (dataFrame.Payload != null)
            {
                if (dataFrame.MessageType == (int)MessageType.PrivateKey)
                {
                    decryptedPayload = Aes.Decrypt(dataFrame.Payload);
                }
                else if (Ses != null)
                {
                    decryptedPayload = Ses.Decrypt(dataFrame.Payload);
                }
                else
                {
                    decryptedPayload = dataFrame.Payload;
                }
            }

            if (dataFrame.MessageType == (int)MessageType.Shutdown)
            {
                _currentState = ConnectionState.Shutdown;
                _transmissionManager.Shutdown();
            }

            if (dataFrame.MessageType == (int)MessageType.ReceiveAcknowledge)
            {
                _transmissionManager.ReportReceivingDataArrivalAcknowledge(dataFrame);
                return;
            }

            if (_highestReceivedSendingId + 1 != dataFrame.RetransmissionId && dataFrame.RetransmissionId != 0)
            {
                _logger.LogDebug($"Message {dataFrame.RetransmissionId} omitted as it has incorrect tranmission id" +
                    $" ({dataFrame.RetransmissionId} but should be {_highestReceivedSendingId + 1})");
                return;
            }

            try
            {
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

                _highestReceivedSendingId = dataFrame.RetransmissionId;

                if (dataFrame.ExpectAcknowledge)
                {
                    SendReceiveAcknowledge(dataFrame.RetransmissionId);
                }
            }
            catch (Exception e)
            {
                _logger.LogError($"Error while processing message number {dataFrame.RetransmissionId} of type" +
                    $" {GetMessageName((int)dataFrame.MessageType)}({(int)dataFrame.MessageType}). {e.Message}");
            }
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
                _logger.LogDebug($"{Id} connection restored");
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
                _logger.LogError("Connection lost");
            CurrentState = ConnectionState.Failed;
        }

        public void FillCurrentEndpoint(IPEndPoint proposedEndpoint)
        {
            if (CurrentEndpoint == null)
            {
                CurrentEndpoint = proposedEndpoint;
            }
            else
            {
                _logger.LogWarning("Trying to fill currentEndpoint when it's already filled");
            }
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
    }
}