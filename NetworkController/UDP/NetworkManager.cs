using ConnectionsManager.Debugging;
using Microsoft.Extensions.Logging;
using NetworkController.DataTransferStructures;
using NetworkController.Helpers;
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
using TransmissionComponent;

namespace NetworkController.UDP
{
    public class NetworkManager : INetworkController, INetworkControllerInternal
    {
        public Guid DeviceId { get; set; } = Guid.NewGuid();
        public int ListenerPort { get; set; }
        private ITransmissionHandler transmissionController;
        private readonly ILogger _logger;

        private List<ExternalNode> _knownNodes = new List<ExternalNode>();
        public IEnumerable<IExternalNode> Nodes
        {
            get
            {
                return _knownNodes.Cast<IExternalNode>();
            }
        }

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

        public int MaxPacketSize
        {
            get
            {
                return transmissionController.MaxPacketSize;
            }
            internal set
            {
                transmissionController.MaxPacketSize = value;
            }
        }

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
            //get { return ((IPEndPoint)udpClient.Client.LocalEndPoint).Port; }
            get { return ListenerPort; } // TODO: remove?
        }

        public IPEndPoint DeviceEndpoint
        {
            get
            {
                return new IPEndPoint(DeviceIPAddress, DevicePort);
            }
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
            if (_persistentStorage == null)
            {
                throw new Exception("Storage not registered");
            }

            foreach (var n in _persistentStorage.Data)
            {
                var newNode = ConnectManually(
                    new IPEndPoint(n.LastIP, n.LastPort),
                    false,
                    n.Id);

                newNode.RestoreSecurityKeys(n.Key, () =>
                {
                    _logger.LogError("Restoring secuirity keys failed. You may need to manually set up connection again.");

                    _persistentStorage.Data.Remove(n);
                    _persistentStorage.Save();
                });
            }
        }

        public IEnumerable<IExternalNodeInternal> GetNodes_Internal()
        {
            return _knownNodes.Cast<IExternalNodeInternal>();
        }

        public IExternalNodeInternal AddNode(Guid id)
        {
            if (_knownNodes.Any(x => x.Id == id))
            {
                _logger.LogError("Tried to add node that is already present");
                return null;
            }

            var connTracker = _tracker.NewSession();

            ExternalNode en = new ExternalNode(id, this, _logger, connTracker, transmissionController);
            _knownNodes.Add(en);
            _logger.LogInformation($"New node added. Number of external nodes: {_knownNodes.Count}");

            OnNodeAddedEvent(null);

            return en;
        }

        public void StartListening(int port = 13000)
        {
            ListenerPort = port;

            transmissionController.StartListening(port);

            transmissionController.NewIncomingMessage = (newMsgEventArgs) =>
            {
                var df = newMsgEventArgs.DataFrame;
                var senderIpEndPoint = newMsgEventArgs.SenderIPEndpoint;

                if (Blacklist.Contains(df.SourceNodeIdGuid))
                {
                    _logger.LogInformation("Rejected message from blacklisted device");
                }
                else
                {

                    IExternalNodeInternal node = _knownNodes.FirstOrDefault(x => x.Id == df.SourceNodeIdGuid);

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
                        if (node.CurrentEndpoint == null ||
                            (!node.CurrentEndpoint.Equals(senderIpEndPoint) && node.CurrentState != ExternalNode.ConnectionState.Ready))
                        {
                            node.FillCurrentEndpoint(senderIpEndPoint);
                        }

                        NC_DataFrame ncdf = NC_DataFrame.Unpack(df.Payload);

                        if (!MessageTypeGroups.IsKeepaliveNoLogicRelatedMessage(ncdf.MessageType))
                        {
                            _logger.LogDebug($"{node.Id} \t Incoming: {GetMessageName(ncdf.MessageType) + " transm. id: " + df.RetransmissionId}");
                        }

                        try
                        {
                            node.HandleIncomingBytes(ncdf);
                        }
                        catch (Exception e)
                        {
                            _logger.LogError($"Error while processing message number {df.RetransmissionId} of type" +
                                $" {GetMessageName((int)ncdf.MessageType)}({(int)ncdf.MessageType}). {e.Message}");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Node rejected. Incoming bytes not handled");
                    }
                }

                return TransmissionComponent.Structures.Other.AckStatus.Failure;

            };
        }

        private string GetMessageName(int msgId)
        {
            foreach (var t in GetMessageTypes())
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

        /// <param name="initializeConnection">True begins handshaking. Set to false if you already have security keys</param>
        public IExternalNode ConnectManually(IPEndPoint endpoint, bool initializeConnection = true, Guid? knownId = null)
        {
            var connTracker = _tracker.NewSession();

            ExternalNode en;
            ExternalNode foundEndpoint = _knownNodes.FirstOrDefault(x => x.CurrentEndpoint == endpoint);
            if (foundEndpoint == null)
            {
                en = new ExternalNode(endpoint, this, _logger, connTracker, transmissionController);
                _knownNodes.Add(en);
                _logger.LogInformation($"New node added. Number of external nodes: {_knownNodes.Count}");

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

        //public void SendBytes(byte[] data, IPEndPoint destination)
        //{
        //    try
        //    {
        //        transmissionController.SendMessageSequentially(destination, data, DeviceId);
        //    }
        //    catch (SocketException e)
        //    {
        //        // possible "The requested address is not valid in its context"

        //        _logger.LogError($"Couldn't SendBytes. Destination: {destination}. Reason: {e.Message}");
        //    }
        //}
    }
}