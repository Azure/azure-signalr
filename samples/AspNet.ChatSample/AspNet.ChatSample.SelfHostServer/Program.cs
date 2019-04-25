﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Owin.Hosting;

namespace AspNet.ChatSample.SelfHostServer
{
    class Program
    {
        static void Main(string[] args)
        {
            if (!(args.Length > 0 && int.TryParse(args[0], out var port)))
            {
                port = 8009;
            }

            var url = $"http://localhost:{port}";
            using (WebApp.Start<Startup>(url))
            {
                Console.WriteLine($"Server running at {url}");
                Console.ReadLine();
            }
        }
    }
}
