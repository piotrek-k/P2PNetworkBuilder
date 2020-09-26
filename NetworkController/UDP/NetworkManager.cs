using ConnectionsManager.Debugging;
using Microsoft.Extensions.Logging;
using NetworkController.Interfaces;
using NetworkController.Interfaces.ForTesting;
using NetworkController.Models;
using NetworkController.Persistance;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace NetworkController.UDP
{
    public class NetworkManager : INetworkController, INetworkControllerInternal
    {
        public Guid DeviceId { get; set; } = Guid.NewGuid();
        public int ListenerPort { get; set; }
        private UdpClient udpClient;
        private readonly ILogger _logger;

        private List<ExternalNode> _knownNodes = new List<ExternalNode>();

        /// <summary>
        /// List of device ids that will be automatically rejected
        /// </summary>
        public List<Guid> Blacklist { get; internal set; } = new List<Guid>();

        /// <summary>
        /// Extended logger. Keeps track of network events
        /// </summary>
        public NetworkBehaviourTracker _tracker;

        public Func<IExternalNode, bool> ConnectionResetRule { get; set; } = (node) => false;
        public Func<Guid, bool> NewUnannouncedConnectionAllowanceRule { get; set; } = (guid) => true;

        public event EventHandler NetworkChanged;

        public virtual void OnNetworkChangedEvent(EventArgs e)
        {
            EventHandler handler = NetworkChanged;
            handler?.Invoke(this, e);
        }

        public event EventHandler NodeAdded;
        public virtual void OnNodeAddedEvent(EventArgs e)
        {
            EventHandler handler = NodeAdded;
            handler?.Invoke(this, e);
        }

        public event EventHandler<HandshakingFinishedEventArgs> NodeFinishedHandshaking;
        public class HandshakingFinishedEventArgs : EventArgs
        {
            public IExternalNode Node { get; set; }
        }
        public virtual void OnNodeFinishedHandshakingEvent(HandshakingFinishedEventArgs e)
        {
            EventHandler<HandshakingFinishedEventArgs> handler = NodeFinishedHandshaking;
            handler?.Invoke(this, e);
        }

        public List<Type> _messageTypes = new List<Type>();
        private IPersistentNodeStorage _persistentStorage;

        public void RegisterMessageTypeEnum(Type type)
        {
            if (type.IsEnum)
            {
                foreach (int v in Enum.GetValues(type).Cast<int>())
                {
                    foreach (var mt in _messageTypes)
                    {
                        if (Enum.IsDefined(mt, v))
                        {
                            throw new Exception("Added MessageTypeEnum has value or name that is already defined");
                        }
                    }
                }

                _messageTypes.Add(type);
            }
            else
            {
                throw new Exception("MessageType should be enumareable");
            }

        }

        public List<Type> GetMessageTypes()
        {
            return _messageTypes;
        }

        public IPAddress DeviceIPAddress
        {
            get
            {
                //return ((IPEndPoint)TcpListener.LocalEndpoint).Address;

                IPAddress output = IPAddress.Any;
                foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (item.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 &&
                        item.OperationalStatus == OperationalStatus.Up)
                    {
                        IPInterfaceProperties adapterProperties = item.GetIPProperties();

                        if (adapterProperties.GatewayAddresses.FirstOrDefault() != null)
                        {
                            foreach (UnicastIPAddressInformation ip in adapterProperties.UnicastAddresses)
                            {
                                if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                                {
                                    output = ip.Address;
                                }
                            }
                        }
                    }
                }

                return output;
            }
        }

        public int DevicePort
        {
            get { return ((IPEndPoint)udpClient.Client.LocalEndPoint).Port; }
        }

        public NetworkManager(ILogger logger, NetworkBehaviourTracker tracker)
        {
            _logger = logger;
            _tracker = tracker;
            RegisterMessageTypeEnum(typeof(MessageType));
        }

        public NetworkManager(ILogger logger, NetworkBehaviourTracker tracker, Guid enforceId) : this(logger, tracker)
        {
            DeviceId = enforceId;
            logger.LogInformation($"Device Id set to {DeviceId}");
        }

        public void RegisterPersistentNodeStorage(IPersistentNodeStorage storage)
        {
            storage.LoadOrCreate();
            NodeFinishedHandshaking += (source, args) =>
            {
                storage.AddNewAndSave(args.Node);
            };
            _persistentStorage = storage;
        }

        public void RestorePreviousSessionFromStorage()
        {
            if(_persistentStorage == null)
            {
                throw new Exception("Storage not registered");
            }

            foreach(var n in _persistentStorage.Data)
            {
                var newNode = ConnectManually(
                    new IPEndPoint(n.LastIP, n.LastPort),
                    false,
                    n.Id);

                newNode.RestoreSecurityKeys(n.Key, () =>
                {
                    _logger.LogError("========= KEY FAILURE");

                    _persistentStorage.Data.Remove(n);
                    _persistentStorage.Save();
                });
            }
        }

        public List<IExternalNode> GetNodes()
        {
            return _knownNodes.Cast<IExternalNode>().ToList();
        }

        public IEnumerable<IExternalNodeInternal> GetNodes_Internal()
        {
            return _knownNodes.Cast<IExternalNodeInternal>();
        }

        public ExternalNode AddNode(Guid id)
        {
            if (_knownNodes.Any(x => x.Id == id))
            {
                _logger.LogError("Node already present");
            }

            var connTracker = _tracker.NewSession();

            ExternalNode en = new ExternalNode(id, this, _logger, connTracker);
            _knownNodes.Add(en);
            _logger.LogDebug($"New node added. Number of external nodes: {_knownNodes.Count}");

            OnNodeAddedEvent(null);

            return en;
        }

        public void StartListening(int port = 13000)
        {
            ListenerPort = port;
            udpClient = new UdpClient(ListenerPort);

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

            udpClient.BeginReceive(new AsyncCallback(handleIncomingMessages), null);
        }

        /// <param name="initializeConnection">True begins handshaking. Set to false if you already have security keys</param>
        public IExternalNode ConnectManually(IPEndPoint endpoint, bool initializeConnection = true, Guid? knownId = null)
        {
            var connTracker = _tracker.NewSession();

            ExternalNode en;
            ExternalNode foundEndpoint = _knownNodes.FirstOrDefault(x => x.CurrentEndpoint == endpoint);
            if (foundEndpoint == null)
            {
                en = new ExternalNode(endpoint, this, _logger, connTracker);
                _knownNodes.Add(en);
                _logger.LogDebug("Created new ExternalNode");

                OnNodeAddedEvent(null);
            }
            else
            {
                _logger.LogDebug("Endpoint already known. Not connecting.");
                en = foundEndpoint;
            }

            if (knownId != null)
            {
                en.SetId(knownId.Value);
            }

            if (initializeConnection)
            {
                en.InitializeConnection();
            }

            return en;
        }

        public void SendBytes(byte[] data, IPEndPoint destination)
        {
            try
            {
                udpClient.Send(data, data.Length, destination);
            }
            catch (SocketException e)
            {
                // possible "The requested address is not valid in its context"

                _logger.LogError($"Couldn't SendBytes. Destination: {destination}. Reason: {e.Message}");
            }
        }

        private void handleIncomingMessages(IAsyncResult ar)
        {
            IPEndPoint senderIpEndPoint = new IPEndPoint(0, 0);
            var receivedData = udpClient.EndReceive(ar, ref senderIpEndPoint);
            DataFrame df = DataFrame.Unpack(receivedData);

            if (Blacklist.Contains(df.SourceNodeIdGuid))
            {
                _logger.LogInformation("Rejected message from blacklisted device");
            }
            else
            {

                var node = _knownNodes.FirstOrDefault(x => x.Id == df.SourceNodeIdGuid);

                if (node == null)
                {
                    // Possibly used manual connection and id is not yet known
                    node = _knownNodes.FirstOrDefault(x =>
                        (x.Id == null || x.Id == Guid.Empty) && x.CurrentEndpoint.Equals(senderIpEndPoint));

                    if (node != null)
                    {
                        node.SetId(df.SourceNodeIdGuid);
                    }
                }

                if (node == null && NewUnannouncedConnectionAllowanceRule(df.SourceNodeIdGuid))
                {
                    node = AddNode(df.SourceNodeIdGuid);
                    node.PublicEndpoint = senderIpEndPoint;
                }

                if (node != null)
                {
                    if (node.CurrentEndpoint == null || !node.CurrentEndpoint.Equals(senderIpEndPoint))
                    {
                        node.FillCurrentEndpoint(senderIpEndPoint);
                    }

                    node.HandleIncomingBytes(df);
                }
                else
                {
                    _logger.LogWarning("Node rejected. Incoming bytes not handled");
                }
            }

            udpClient.BeginReceive(new AsyncCallback(handleIncomingMessages), null);
        }
    }
}