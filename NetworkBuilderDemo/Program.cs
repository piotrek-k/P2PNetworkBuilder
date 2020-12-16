using Microsoft.Extensions.Logging;
using NetworkController;
using NetworkController.Debugging;
using NetworkController.Interfaces;
using NetworkController.Persistance;
using NetworkController.UDP;
using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using static NetworkController.UDP.NetworkManager;

namespace NetworkBuilderDemo
{
    class Program
    {
        static readonly CancellationTokenSource applicationExitTokenSource = new CancellationTokenSource();
        private static INetworkController network;

        static async Task Main(string[] args)
        {
            // run command example:
            // ./NetworkBuilderDemo 13001 9e6f77c3-3b80-4c95-a547-44f96aca0044 ./keys.txt

            if (args.Count() < 3)
            {
                Console.WriteLine("3 arguments needed:");
                Console.WriteLine("[port] [guid] [file_name]");
            }

            // This logger prints logs to console output
            ILogger logger = new CustomLoggerProvider(LogLevel.Trace).CreateLogger("");

            // Creating NetworkController instance
            INetworkController network = new NetworkManagerFactory()
                // optional: add your custom logger. By default it prints to console
                .AddLogger(logger)
                // optional: store information about connected nodes and encryption keys in txt file
                .AddPersistentNodeStorage(new PlainTextFileNodeStorage(args[2]))
                // optional: specify max size of packet. When exceeded, exception will be thrown
                //.SetMaxPacketSize(1234)
                .AddNewUnannouncedConnectionAllowanceRule((source_id) =>
                {
                    // unknown device want to connect to network

                    // you should perform some verification steps here
                    // e.g. display prompt to user
                    // use cautiously, possible security vulnerability

                    return true; // to give device access to network
                    // return false; // to reject it
                })
                .AddConnectionResetRule((node) =>
                {
                    // known device lost encryption key and wants to reconnect

                    // use cautiously, possible security vulnerability

                    return true; // to give device access to network
                    // return false; // to reject it
                })
                .Create();

            int parsedPort;
            try
            {
                parsedPort = int.Parse(args[0]);
            }
            catch (Exception)
            {
                Console.WriteLine("Incorrect port number");
                return;
            }

            Guid parsedGuid;
            try
            {
                parsedGuid = Guid.Parse(args[1]);
            }
            catch (Exception)
            {
                Console.WriteLine("Incorrect id of device");
                return;
            }
            network.DeviceId = parsedGuid;

            network.StartListening(parsedPort);

            // if you used AddPersistentNodeStorage to store session to txt file,
            // you can load it now
            network.RestorePreviousSessionFromStorage();

            network.NetworkChanged += (object sender, EventArgs e) =>
            {
                Console.WriteLine("State of one of nodes has changed");
            };
            network.NodeAdded += (object sender, EventArgs e) =>
            {
                Console.WriteLine("New node has been added (but it's not ready to talk to yet)");
            };
            network.NodeFinishedHandshaking += (object sender, HandshakingFinishedEventArgs e) =>
            {
                Console.WriteLine("New node has been added (and it's ready to communicate)");
            };

            // You can prevent some devices from connecting
            // network.Blacklist.Add(someGuid);

            // to ensure that your message ids doesn't collide with those used by NetworkController
            // register them here. If they do, exception will be thrown.
            network.RegisterMessageTypeEnum(typeof(SomeCustomMessageTypes));

            Console.WriteLine(
                $"\n====== DEVICE INFO ======\n" +
                $"Port number: {parsedPort}\n" +
                $"DeviceId: {network.DeviceId}\n" +
                $"=========================\n");

            PrintHelp();

            new Thread(() =>
            {
                string input = "";
                IExternalNode currentNode = null;
                while (input.Trim() != "q")
                {
                    try
                    {
                        input = Console.ReadLine();

                        string[] inputArgs = input.Split(' ');

                        switch (inputArgs[0])
                        {
                            case "help":
                            case "h":
                                PrintHelp();
                                break;
                            case "showNetwork":
                            case "sn":
                                Console.WriteLine("\n==================");
                                Console.WriteLine("Currently connected nodes:\n");
                                Console.WriteLine("[Index] [Id] [Endpoint] [State]\n");
                                int i = 0;
                                foreach (var n in network.Nodes)
                                {
                                    Console.WriteLine($"\t - ({i}) {n.Id}, {n.CurrentEndpoint}, " +
                                        $"{Enum.GetName(typeof(NetworkController.UDP.ExternalNode.ConnectionState), n.CurrentState)}");
                                    i++;
                                }
                                Console.WriteLine("==================");
                                break;
                            case "connect":
                            case "ct":
                                IPEndPoint.TryParse(inputArgs[1], out var newParsedIP);
                                if (newParsedIP != null)
                                {
                                    network.ConnectManually(newParsedIP, true);
                                }
                                else
                                {
                                    Console.WriteLine("Problem with parsing IP:Port");
                                }
                                Console.WriteLine("OK");
                                break;
                            case "chooseNode":
                                if (inputArgs.Length == 2)
                                {
                                    currentNode = network.Nodes.ElementAt(int.Parse(inputArgs[1]));
                                    Console.WriteLine($"Chosen node: {currentNode.Id}");
                                }
                                else
                                {
                                    throw new Exception("Wrong number of arguments");
                                }
                                break;
                            case "send":
                                if (currentNode == null)
                                {
                                    throw new Exception("Choose node");
                                }
                                currentNode.SendMessageSequentially((int)SomeCustomMessageTypes.Test1, new byte[] { 0, 1, 2, 3, 4, 5 });
                                break;
                            case "send2":
                                if (currentNode == null)
                                {
                                    throw new Exception("Choose node");
                                }
                                currentNode.SendMessageNonSequentially((int)SomeCustomMessageTypes.Test2, new byte[] { 0, 1, 2, 3, 4, 5 });
                                break;
                            case "send3":
                                if (currentNode == null)
                                {
                                    throw new Exception("Choose node");
                                }
                                currentNode.SendAndForget((int)SomeCustomMessageTypes.Test2, new byte[] { 0, 1, 2, 3, 4, 5 });
                                break;
                            case "st1":
                                if (currentNode == null)
                                {
                                    throw new Exception("Choose node");
                                }
                                Console.WriteLine("You can top testing by pressing 'q'. Type 'ok' to begin.");
                                if (Console.ReadLine().Trim().ToLower().Equals("ok"))
                                {
                                    try
                                    {
                                        ConsoleKeyInfo key;
                                        int counterOfSentMessages = 0;
                                        int counterOfReceivedMessages = 0;
                                        do
                                        {
                                            while (!Console.KeyAvailable)
                                            {

                                                counterOfSentMessages++;

                                                currentNode.SendMessageSequentially((int)SomeCustomMessageTypes.Test1, new byte[] { 0, 1, 2, 3, 4, 5 }, (status) =>
                                                {
                                                    counterOfReceivedMessages++;
                                                    if (status != TransmissionComponent.Structures.Other.AckStatus.Success)
                                                    {
                                                        throw new Exception("Message not processed successfully");
                                                    }
                                                });
                                            }
                                        } while (Console.ReadKey(true).Key != ConsoleKey.Q);

                                        Console.WriteLine("Waiting for callbacks...");

                                        do
                                        {
                                            Thread.Sleep(1000);
                                        } while (Console.ReadKey(true).Key != ConsoleKey.Q || counterOfReceivedMessages == counterOfSentMessages);
                                    }
                                    catch (Exception e)
                                    {
                                        Console.WriteLine($"Cought exception: {e.Message}");
                                    }
                                }
                                break;
                            default:
                                Console.WriteLine("No such command");
                                break;
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Couldn't process command: {e.Message}");
                    }
                }
            }).Start();

            await Task.Delay(Timeout.Infinite, applicationExitTokenSource.Token);
        }

        static void PrintHelp()
        {
            Console.WriteLine($"" +
               $"Available commands:\n" +
               $"==========NETWORK=========\n" +
               $"* sn - show network\n" +
               $"* ct [ip:port] - connect to node\n" +
               $"* chooseNode [index] - choose node (you can see indexes by typing 'sn')" +
               $"* send - send example message (ordering + retransmission)\n" +
               $"* send2 - send example message (no ordering, just retramission)\n" +
               $"* send3 - send example message (no ordering, no retranssmission, just UDP packet)\n" +
               $"* st1 - stress testing - using ordered (sequential) messages\n" +
               $"==========================\n\n");
        }
    }
}
