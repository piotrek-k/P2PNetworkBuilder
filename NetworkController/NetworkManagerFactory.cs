using ConnectionsManager.Debugging;
using Microsoft.Extensions.Logging;
using NetworkController.Debugging;
using NetworkController.Interfaces;
using NetworkController.Persistance;
using NetworkController.UDP;
using System;

namespace NetworkController
{
    public class NetworkManagerFactory
    {
        private ILogger _logger;
        private NetworkBehaviourTracker _nbt;
        private Func<IExternalNode, bool> _connectionResetRule;
        private Func<Guid, bool> _newUnannouncedConnectionAllowanceRule;
        private IPersistentNodeStorage _nodeStorage;
        private int? _maxPacketSize = null;

        public NetworkManagerFactory()
        {
        }

        public NetworkManagerFactory AddLogger(ILogger logger)
        {
            _logger = logger ?? new CustomLoggerProvider().CreateLogger("category name");

            return this;
        }

        public NetworkManagerFactory AddTracker(NetworkBehaviourTracker nbt)
        {
            _nbt = nbt ?? new NetworkBehaviourTracker();

            return this;
        }

        public NetworkManagerFactory AddConnectionResetRule(Func<IExternalNode, bool> rule)
        {
            _connectionResetRule = rule;

            return this;
        }

        public NetworkManagerFactory AddNewUnannouncedConnectionAllowanceRule(Func<Guid, bool> rule)
        {
            _newUnannouncedConnectionAllowanceRule = rule;

            return this;
        }

        public NetworkManagerFactory AddPersistentNodeStorage(IPersistentNodeStorage storage)
        {
            _nodeStorage = storage;

            return this;
        }

        /// <summary>
        /// Some devices reject too large UDP packets or divide them into smaller chunks.
        /// To avoid it, set packet size limit
        /// </summary>
        public NetworkManagerFactory SetMaxPacketSize(int size)
        {
            _maxPacketSize = size;

            return this;
        }

        public INetworkController Create(Guid? enforceId = null)
        {
            NetworkManager nm;

            if(_nbt == null)
            {
                _nbt = new NetworkBehaviourTracker();
            }

            if(_logger == null)
            {
                _logger = new CustomLoggerProvider().CreateLogger("category name");
            }

            if (enforceId == null || enforceId.Value == Guid.Empty)
            {
                nm = new NetworkManager(_logger, _nbt);
            }
            else
            {
                nm = new NetworkManager(_logger, _nbt, enforceId.Value);
            }

            if (_connectionResetRule != null)
            {
                nm.ConnectionResetRule = _connectionResetRule;
            }

            if(_newUnannouncedConnectionAllowanceRule != null)
            {
                nm.NewUnannouncedConnectionAllowanceRule = _newUnannouncedConnectionAllowanceRule;
            }

            if(_nodeStorage != null)
            {
                nm.RegisterPersistentNodeStorage(_nodeStorage);
            }

            if(_maxPacketSize != null)
            {
                nm.MaxPacketSize = _maxPacketSize.Value;
            }

            return nm;
        }
    }
}