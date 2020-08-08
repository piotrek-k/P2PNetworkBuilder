using NetworkController;
using NetworkController.Interfaces;
using NetworkController.UDP;
using System;
using System.Net;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkBuilderDemo
{
    class Program
    {
        static readonly CancellationTokenSource applicationExitTokenSource = new CancellationTokenSource();
        private static INetworkController network;

        static async Task Main(string[] args)
        {
            network = new NetworkManagerFactory()
                .Create();

            foreach(var a in args)
            {
                Console.WriteLine(a);
            }
            

            int parsedPort;
            if (args.Length < 1 || !int.TryParse(args[0], out parsedPort)){
                parsedPort = 13000;
            }

            network.StartListening(parsedPort);

            IPEndPoint parsedIP;
            if (args.Length >= 2 && IPEndPoint.TryParse(args[1], out parsedIP))
            {
                network.ConnectManually(parsedIP);
                Console.WriteLine("Connecting...");
            }

            network.NetworkChanged += (object sender, EventArgs e) =>
            {
                ListRefresh();
            };

            await Task.Delay(Timeout.Infinite, applicationExitTokenSource.Token);
        }

        private static void ListRefresh()
        {
            Console.WriteLine("Id \t State \t Address");

            foreach (var n in network.GetNodes())
            {
                Console.WriteLine($"{n.Id} \t {Enum.GetName(typeof(ExternalNode.ConnectionState), n.CurrentState)} \t {n.CurrentEndpoint}");
            }
        }
    }
}
