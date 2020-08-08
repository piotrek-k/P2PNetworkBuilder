using ConnectionsManager.Debugging;
using Microsoft.Extensions.Logging;
using NetworkController.Debugging;
using NetworkController.Interfaces;
using NetworkController.UDP;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkController
{
    public class NetworkManagerFactory
    {
        private ILogger _logger;
        private NetworkBehaviourTracker _nbt;
        private Func<bool> _connectionResetRule;

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
            _nbt = nbt ?? new ConnectionsManager.Debugging.NetworkBehaviourTracker();

            return this;
        }

        public NetworkManagerFactory AddConnectionResetRule(Func<bool> rule)
        {
            _connectionResetRule = rule;

            return this;
        }

        public INetworkController Create(Guid? enforceId = null)
        {
            NetworkManager nm;
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

            return nm;
        }
    }
}