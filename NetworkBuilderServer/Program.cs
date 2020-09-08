using NetworkController;
using NetworkController.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkBuilderServer
{
    class Program
    {
        static readonly CancellationTokenSource applicationExitTokenSource = new CancellationTokenSource();

        static async Task Main(string[] args)
        {
            int parsedPort = 0;
            
            try
            {
                parsedPort = int.Parse(args[0]);
            }
            catch (Exception)
            {
                Console.WriteLine("Error while parsing arguments");
                Console.WriteLine("Expected:");
                Console.WriteLine("[listener_port]");
            }

            INetworkController network = new NetworkManagerFactory()
                .AddConnectionResetRule((externalNode) =>
                {
                    return true;
                })
                .AddNewUnannouncedConnectionAllowanceRule((guid) =>
                {
                    Console.WriteLine($"{guid} wants to connect. y/n?");
                    string answer = Console.ReadLine();
                    if(answer == "y")
                    {
                        Console.WriteLine("Allowed");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine("Rejected");
                        return false;
                    }
                })
                .Create();

            network.StartListening(parsedPort);

            Console.WriteLine("Working");

            await Task.Delay(Timeout.Infinite, applicationExitTokenSource.Token);
        }
    }
}
