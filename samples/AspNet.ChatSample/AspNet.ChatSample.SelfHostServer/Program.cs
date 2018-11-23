// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Owin.Hosting;

namespace AspNet.ChatSample.SelfHostServer
{
    class Program
    {
        static void Main(string[] args)
        {
            using (WebApp.Start<Startup>("http://localhost:8009"))
            {
                Console.WriteLine("Server running at http://localhost:8009/");
                Console.ReadLine();
            }
        }
    }
}
