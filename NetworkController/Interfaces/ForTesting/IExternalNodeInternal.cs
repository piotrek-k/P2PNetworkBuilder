using NetworkController.Encryption;
using NetworkController.Models;
using NetworkController.UDP;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using static NetworkController.UDP.ExternalNode;

namespace NetworkController.Interfaces.ForTesting
{
    public interface IExternalNodeInternal : IExternalNode
    {
        void HandleIncomingBytes(DataFrame dataFrame);

        AsymmetricEncryptionService Aes { get; set; }
        SymmetricEncryptionService Ses { get; set; }
        INetworkControllerInternal NetworkController { get; set; }

        IPEndPoint PublicEndpoint { get; }
        IPEndPoint ClaimedPrivateEndpoint { get; set; }

        void SendBytes(int type, byte[] payloadOfDataFrame, IPEndPoint endpoint, bool ensureDelivered);
        void SendReceiveAcknowledge(uint retransmissionId);

        public new ConnectionState CurrentState { get; set; }

        bool AfterHolePunchingResponse_WaitingForPingResponse { get; set; }

        void SendPingSeries();

        void InitializeConnection();
        void ReportIncomingPingResponse();
        void ReportConnectionFailure();
        void ReportThatConnectionIsSetUp();
    }
}
