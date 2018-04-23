﻿using System;
using Microsoft.Azure.SignalR;

namespace RestAPISample
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 1  && args.Length != 2)
            {
                Console.WriteLine("Please specify <hubName> [<connectionString>]");
                Console.WriteLine($"If <connecitonString> is not specified, we will read it from environment key {ServiceOptions.ConnectionStringDefaultKey}");
                return;
            }
            BroadcastTimer broadcastTimer = null;
            if (args.Length == 2)
            {
                broadcastTimer = new BroadcastTimer(args[1], args[0]);
            }
            else
            {
                broadcastTimer = new BroadcastTimer(args[0]);
            }
            Console.WriteLine("Press any key to stop...");
            broadcastTimer.Start();
            Console.ReadLine();
            broadcastTimer.Stop();
        }
    }
}
