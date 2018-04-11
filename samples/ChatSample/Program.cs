// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace ChatSample
{
    public class Program
    {
        public static void Main(string[] args)
        {
            new WebHostBuilder()
                .UseSetting(WebHostDefaults.PreventHostingStartupKey, "true")
                .ConfigureLogging((context, factory) =>
                {
                    factory.AddConfiguration(context.Configuration.GetSection("Logging"));
                    factory.AddConsole();
                    factory.AddDebug();
                })
                .UseKestrel()
                .UseUrls("http://*:5050/")
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseStartup<Startup>()
                .Build()
                .Run();
        }
    }
}
