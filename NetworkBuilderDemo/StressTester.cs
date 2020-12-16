using NetworkController.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace NetworkBuilderDemo
{
    public static class StressTester
    {
        public class StressTesterParams
        {
            public int CounterOfSentMessages = 0;
            public int CounterOfReceivedMessages = 0;
        }
        public static void StartTesting(IExternalNode currentNode, Action<StressTesterParams> sendMessage)
        {
            if (currentNode == null)
            {
                throw new Exception("Choose node");
            }
            Console.WriteLine("You can top testing by pressing 'q'. Type 'ok' to begin.");
            if (Console.ReadLine().Trim().ToLower().Equals("ok"))
            {
                try
                {
                    StressTesterParams st_values = new StressTesterParams();
                    st_values.CounterOfSentMessages = 0;
                    st_values.CounterOfReceivedMessages = 0;
                    var thread = new Thread(() =>
                    {
                        do
                        {
                            while (!Console.KeyAvailable)
                            {

                                st_values.CounterOfSentMessages++;

                                sendMessage(st_values);
                            }
                        } while (Console.ReadKey(true).Key != ConsoleKey.Q);
                    });
                    //thread.IsBackground = true;
                    thread.Start();
                    thread.Join();

                    do
                    {
                        Thread.Sleep(1000);
                        Console.WriteLine($"Waiting for callbacks. Received {st_values.CounterOfReceivedMessages} out of {st_values.CounterOfSentMessages}");
                    } while (st_values.CounterOfReceivedMessages != st_values.CounterOfSentMessages); //Console.ReadKey(true).Key != ConsoleKey.Q || 
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Cought exception: {e.Message}");
                }
            }
        }
    }
}
