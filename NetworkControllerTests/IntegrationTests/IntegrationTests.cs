using ConnectionsManager.Debugging;
using ConnectionsManager.Tests;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Xunit;
using Xunit.Abstractions;

namespace NetworkControllerTests.IntegrationTests
{
    public class IntegrationTests
    {
        private ILogger logger;


        public IntegrationTests(ITestOutputHelper output)
        {
            logger = new LogToOutput(output, "logger").CreateLogger("category");
        }

        [Fact]
        public void Nodes_Should_Discover_Each_Other()
        {
            NetworkBehaviourTracker nbtRN = new NetworkBehaviourTracker();
            NetworkController.UDP.NetworkManager rendezvousNode = new NetworkController.UDP.NetworkManager(logger, nbtRN);
            rendezvousNode.StartListening(13000);

            List<NetworkController.UDP.NetworkManager> networkControllers = new List<NetworkController.UDP.NetworkManager>();

            for (var i = 1; i < 5; i++)
            {
                new Thread(() =>
                {
                    NetworkBehaviourTracker nbt = new NetworkBehaviourTracker();
                    NetworkController.UDP.NetworkManager nc = new NetworkController.UDP.NetworkManager(logger, nbt);
                    networkControllers.Add(nc);
                    nc.StartListening(13000 + i);
                    nc.ConnectManually(new IPEndPoint(IPAddress.Loopback, 13000));
                }).Start();
                Thread.Sleep(100);
            }

            Thread.Sleep(15000);

            Assert.Equal(4, rendezvousNode.GetNodes().Count());
            foreach (var n in networkControllers)
            {
                Assert.Equal(4, n.GetNodes().Count());
            }
        }
    }
}
