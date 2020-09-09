// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Emulator
{
    public class Program
    {
        private static readonly string SettingsFile = "settings.json";
        private static readonly string ProgramDirectory = Path.GetDirectoryName(typeof(Program).Assembly.Location);
        private static readonly string ProgramDefaultSettingsFile = Path.Combine(ProgramDirectory, SettingsFile);
        private static readonly string AppSettingsFile = Path.Combine(ProgramDirectory, "appsettings.json");

        public static void Main(string[] args)
        {
            // todo: "upstream init" to generate the default setting.json file
            // todo: "upstream list" to list the settings file
            // todo: "start" to run the emulator
            // todo: "help" to explain the upstream and pattern rules

            var host = CreateHostBuilder(args).Build();

            var app = new CommandLineApplication();
            app.Name = "asrs-emulator";
            app.Description = "The local emulator for Azure SignalR Serverless features.";
            app.HelpOption("-h|--help");
            
            app.Command("upstream", command =>
            {
                command.Description = "To init/list/update the upstream options";
                command.HelpOption("-h|--help");
                command.Command("init", c =>
                {
                    c.Description = "Init the default upstream options into a settings.json config";
                    c.OnExecute(() =>
                    {
                        if (File.Exists(SettingsFile))
                        {
                            Console.WriteLine($"Already contains {SettingsFile}, still want to override it with the default one? (N/y)");
                            if (Console.ReadKey().Key != ConsoleKey.Y)
                            {
                                return 0;
                            }
                        }

                        File.Copy(ProgramDefaultSettingsFile, SettingsFile, true);
                        Console.WriteLine($"Exported default settings to {Path.GetFullPath(SettingsFile)}.");
                        return 0;
                    });
                });
                command.Command("list", c =>
                {
                    c.Description = "List current upstream options.";
                    c.OnExecute(() =>
                    {
                        var option = host.Services.GetRequiredService<IOptions<UpstreamOptions>>();
                        option.Value.Print();
                        return 0;
                    });
                });
            });

            app.Command("start", command =>
            {
                command.Description = "To start the emulator.";
                var portOptions = command.Option("-p|--port", "Specify the port to use.", CommandOptionType.SingleValue);
                command.HelpOption("-h|--help");
                command.OnExecute(() =>
                {
                    if (portOptions.HasValue())
                    {
                        var val = portOptions.Value();

                        if (int.TryParse(val, out var port))
                        {
                            host = CreateHostBuilder(args, port).Build();
                        }
                        else
                        {
                            Console.WriteLine($"Invalid port value: {val}.");
                            return 1;
                        }
                    }

                    host.Run();
                    return 0;
                });
            });

            app.OnExecute(() =>
            {
                app.ShowHelp();
                return 0;
            });

            try
            {
                app.Execute(args);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error starting emulator: {e.Message}.");
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args, int? port = null)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();

                    if (port.HasValue)
                    {
                        webBuilder.UseKestrel(o => o.ListenLocalhost(port.Value));
                    }
                })
                .ConfigureAppConfiguration(s =>
                {
                    s.AddJsonFile(AppSettingsFile, optional: true, reloadOnChange: true);
                    s.AddJsonFile(SettingsFile, optional: true, reloadOnChange: true);
                });
        }
    }
}
