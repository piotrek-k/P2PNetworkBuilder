using NetworkController;
using NetworkController.Interfaces;
using NetworkController.Persistance;
using NetworkController.UDP;
using System;
using System.Linq;
using System.Net;
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
            // .\NetworkBuilderDemo.exe 13001 9e6f77c3-3b80-4c95-a547-44f96aca0044 .\keys.txt

            network = new NetworkManagerFactory()
                //.AddPersistentNodeStorage(new PlainTextFileNodeStorage("./keys.txt"))
                .AddPersistentNodeStorage(new PlainTextFileNodeStorage(args[2]))
                .AddConnectionResetRule((node) =>
                {
                    return true;
                })
                .Create();

            foreach (var a in args)
            {
                Console.WriteLine(a);
            }


            int parsedPort;
            if (args.Length < 1 || !int.TryParse(args[0], out parsedPort))
            {
                parsedPort = 13000;
            }

            Guid parsedGuid;
            if (Guid.TryParse(args[1], out parsedGuid))
            {
                network.DeviceId = parsedGuid;
            }

            network.StartListening(parsedPort);

            network.RestorePreviousSessionFromStorage();

            IPEndPoint parsedIP;
            if (args.Length >= 3 && IPEndPoint.TryParse(args[2], out parsedIP) && network.Nodes.Count() == 0)
            {
                network.ConnectManually(parsedIP);
                Console.WriteLine($"Connecting to {parsedIP}...");
            }

            network.NetworkChanged += (object sender, EventArgs e) =>
            {
                ListRefresh();
            };

            network.RegisterMessageTypeEnum(typeof(SomeCustomMessageTypes));

            new Thread(() =>
            {
                IExternalNode currentNode = null;
                int counter = 0;

                while (true)
                {
                    var currentKey = Console.ReadKey();

                    if (currentKey.Key == ConsoleKey.S && currentNode != null)
                    {
                        Console.WriteLine("Sending...");
                        currentNode.SendMessageSequentially(100, new byte[] { 0, 1, 2, 3, 4, 5 });
                    }
                    else if (currentKey.Key == ConsoleKey.D && currentNode != null)
                    {
                        Console.WriteLine("Sending...");
                        currentNode.SendMessageNonSequentially(101, new byte[] { 0, 1, 2, 3, 4, 5 });
                    }
                    else if (currentKey.Key == ConsoleKey.N)
                    {
                        currentNode = network.Nodes.ToList()[counter];
                        counter = counter % network.Nodes.Count();
                        Console.WriteLine($"Set current node to {counter}: {currentNode.Id}");
                    }
                    else if (currentKey.Key == ConsoleKey.R)
                    {
                        if (currentNode == null)
                        {
                            Console.WriteLine("Current node is null");
                        }
                        else
                        {
                            currentNode.RestartConnection();
                        }
                    }
                    else if(currentKey.Key == ConsoleKey.P)
                    {
                        ListRefresh();
                    }
                    else if (currentKey.Key == ConsoleKey.G)
                    {
                        var key = currentNode.GetSecurityKeys();
                        Console.WriteLine(ByteArrayToString(key));
                    }
                    else if (currentKey.Key == ConsoleKey.Z)
                    {
                        Console.WriteLine("Address:");
                        var ep = Console.ReadLine();
                        IPEndPoint newParsedIP;
                        IExternalNode newNode;
                        if (IPEndPoint.TryParse(ep, out newParsedIP))
                        {
                            newNode = network.ConnectManually(newParsedIP);
                        }
                    }
                    else if (currentKey.Key == ConsoleKey.C)
                    {
                        Console.WriteLine("Address:");
                        var ep = Console.ReadLine();
                        Console.WriteLine("Key:");
                        var keyStr = Console.ReadLine();
                        Console.WriteLine("ID:");
                        var id = Guid.Parse(Console.ReadLine());

                        byte[] key = StringToByteArray(keyStr);
                        bool manualKeyRestoring = true;

                        if (key.Count() == 0)
                        {
                            manualKeyRestoring = false;
                        }

                        IPEndPoint newParsedIP;
                        IPEndPoint.TryParse(ep, out newParsedIP);
                        IExternalNode newNode;
                        if (newParsedIP != null)
                        {
                            newNode = network.ConnectManually(newParsedIP, !manualKeyRestoring, id);
                            if (manualKeyRestoring)
                            {
                                newNode.RestoreSecurityKeys(key, () =>
                                {
                                    Console.WriteLine("========= KEY FAILURE");
                                });
                            }
                        }
                    }
                    else if (currentKey.Key == ConsoleKey.Q)
                    {
                        break;
                    }
                }
            }).Start();

            await Task.Delay(Timeout.Infinite, applicationExitTokenSource.Token);
        }

        private static void ListRefresh()
        {
            Console.WriteLine("Id \t State \t Address");

            foreach (var n in network.Nodes)
            {
                Console.WriteLine($"{n.Id} \t {Enum.GetName(typeof(ExternalNode.ConnectionState), n.CurrentState)} \t {n.CurrentEndpoint}");
            }
        }

        public static string ByteArrayToString(byte[] ba)
        {
            return BitConverter.ToString(ba).Replace("-", "");
        }

        public static byte[] StringToByteArray(String hex)
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }
    }
}
