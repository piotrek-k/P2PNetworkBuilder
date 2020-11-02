using Microsoft.Extensions.Logging;
using NetworkController.Interfaces.ForTesting;
using NetworkController.Models;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;

namespace NetworkController.Threads
{
    public class KeepaliveThread
    {
        private const int MsBetweenInitialPings = 1000;
        private const int MsBetweenKeepalivePings = 7000;
        private const int MsToWaitForResponseCheck = 1000;
        private const int MsBetweenPingsWhenNoResponse = 1000;
        
        private int TempWaitingTime = MsBetweenKeepalivePings - MsBetweenInitialPings;
        private readonly IExternalNodeInternal _externalNode;
        private readonly CancellationToken _cts;
        private readonly ILogger _logger;

        private DateTimeOffset _lastRequestTime;
        private DateTimeOffset _lastResponseTime;

        private int _failedPingAttempts = 0;

        private object _keepaliveThreadLock = new object();
        private bool _keepaliveThreadActive = false;

        public KeepaliveThread(IExternalNodeInternal externalNode, CancellationToken cts, ILogger logger)
        {
            _externalNode = externalNode;
            _cts = cts;
            _logger = logger;
        }

        public void InitialConnectionPings()
        {
            new Thread(() =>
            {
                for (int x = 0; x < 5; x++)
                {
                    _externalNode.SendBytes((int)MessageType.Ping, null, _externalNode.ClaimedPrivateEndpoint, false);

                    Thread.Sleep(MsBetweenInitialPings);
                }
            }).Start();

            new Thread(() =>
            {
                for (int x = 0; x < 5; x++)
                {
                    _externalNode.SendBytes((int)MessageType.Ping, null, _externalNode.PublicEndpoint, false);

                    Thread.Sleep(MsBetweenInitialPings);
                }
            }).Start();
        }

        public void BeginKeepaliveThread()
        {
            lock (_keepaliveThreadLock)
            {
                if(_keepaliveThreadActive)
                {
                    return;
                }
                else
                {
                    _keepaliveThreadActive = true;
                }
            }

            _lastResponseTime = DateTimeOffset.Now;

            new Thread(() =>
            {
                bool stopThread = false;
                while(!_cts.IsCancellationRequested && !stopThread)
                {
                    _externalNode.SendAndForget((int)MessageType.Ping, null);

                    _lastRequestTime = DateTimeOffset.Now;
                    Thread.Sleep(MsToWaitForResponseCheck);

                    if(_lastRequestTime > _lastResponseTime)
                    {
                        TempWaitingTime = MsBetweenPingsWhenNoResponse;
                        _failedPingAttempts++;

                        if(_failedPingAttempts > 5)
                        {
                            _externalNode.ReportConnectionFailure();
                            stopThread = true;
                            _keepaliveThreadActive = false;
                        }
                    }
                    else
                    {
                        TempWaitingTime = MsBetweenKeepalivePings - MsBetweenInitialPings;
                        if(TempWaitingTime < 0)
                        {
                            TempWaitingTime = 0;
                        }
                    }

                    Thread.Sleep(TempWaitingTime);
                }

                _logger.LogTrace("Keepalive thread finished");
                _failedPingAttempts = 0;
            }).Start();
        }

        public void InformAboutResponse()
        {
            _lastResponseTime = DateTimeOffset.Now;
            _failedPingAttempts = 0;
        }
    }
}
