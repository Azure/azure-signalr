// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Azure.SignalR.Emulator
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // todo: "upstream init" to generate the default setting.json file
            // todo: "upstream list" to list the settings file
            // todo: "start" to run the emulator
            // todo: "help" to explain the upstream and pattern rules
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            var settingsFile = Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location), "appsettings.json");
            return Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                })
                .ConfigureAppConfiguration(s => s.AddJsonFile(settingsFile, optional: true, reloadOnChange: true))
                .ConfigureAppConfiguration(s => s.AddJsonFile("settings.json", optional: true, reloadOnChange: true));
        }
    }
}
