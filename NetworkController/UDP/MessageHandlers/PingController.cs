using ConnectionsManager.MessageFramework;
using FrameworkPrototype;
using Microsoft.Extensions.Logging;
using NetworkController.Interfaces.ForTesting;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkController.UDP.MessageHandlers
{
    public class PingController : MessageController
    {
        public PingController(ILogger logger) : base(logger)
        {}

        [IncomingMessage(Models.MessageType.Ping)]
        public void IncomingPing(IExternalNodeInternal source, byte[] bytes)
        {
            source.SendAndForget((int)Models.MessageType.PingResponse, null);
        }

        [IncomingMessage(Models.MessageType.PingResponse)]
        public void IncomingPingResponse(IExternalNodeInternal source, byte[] bytes)
        {
            source.ReportIncomingPingResponse();

            if (source.AfterHolePunchingResponse_WaitingForPingResponse)
            {
                source.InitializeConnection();
                source.AfterHolePunchingResponse_WaitingForPingResponse = false;
            }
        }
    }
}
