using ConnectionsManager.Debugging;
using Microsoft.Extensions.Logging;
using NetworkController.Interfaces;
using NetworkController.Interfaces.ForTesting;
using NetworkController.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace NetworkController.UDP
{
    public class NetworkManager : INetworkController, INetworkControllerInternal
    {
        private List<ExternalNode> _knownNodes = new List<ExternalNode>();
        public int ListenerPort { get; set; }
        private UdpClient udpClient;
        private readonly ILogger _logger;

        public Guid DeviceId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Extended logger. Keeps track of network events
        /// </summary>
        public NetworkBehaviourTracker _tracker;

        public Func<IExternalNode, bool> ConnectionResetRule { get; set; } = (node) => true;
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

        public List<Type> _messageTypes = new List<Type>();

        public void RegisterMessageTypeEnum(Type type)
        {
            _messageTypes.Add(type);
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

        public IEnumerable<IExternalNode> GetNodes()
        {
            return _knownNodes.Cast<IExternalNode>();
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

        public void ConnectManually(IPEndPoint endpoint)
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
            en.InitializeConnection();
        }

        public void SendBytes(byte[] data, IPEndPoint destination)
        {
            udpClient.Send(data, data.Length, destination);
        }

        private void handleIncomingMessages(IAsyncResult ar)
        {
            IPEndPoint senderIpEndPoint = new IPEndPoint(0, 0);
            var receivedData = udpClient.EndReceive(ar, ref senderIpEndPoint);
            DataFrame df = DataFrame.Unpack(receivedData);

            var node = _knownNodes.FirstOrDefault(x => x.Id == df.SourceNodeId);

            if (node == null)
            {
                // Possibly used manual connection and id is not yet known
                node = _knownNodes.FirstOrDefault(x =>
                    (x.Id == null || x.Id == Guid.Empty) && x.CurrentEndpoint.Equals(senderIpEndPoint));

                if (node != null)
                {
                    node.SetId(df.SourceNodeId);
                }
            }

            if (node == null && NewUnannouncedConnectionAllowanceRule(df.SourceNodeId))
            {
                node = AddNode(df.SourceNodeId);
                node.PublicEndpoint = senderIpEndPoint;
            }

            if (node != null)
            {
                if (node.CurrentEndpoint == null)
                {
                    node.FillCurrentEndpoint(senderIpEndPoint);
                }

                node.HandleIncomingBytes(df);
            }
            else
            {
                _logger.LogWarning("Node rejected. Incoming bytes not handled");
            }

            udpClient.BeginReceive(new AsyncCallback(handleIncomingMessages), null);
        }
    }
}