// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Net;
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
        private const int DefaultPort = 8888;
        private static readonly string SettingsFileName = "settings.json";
        private static readonly string SettingsFile = Path.GetFullPath(SettingsFileName);
        private static readonly string AppSettingsFile = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        internal static readonly string ProgramDefaultSettingsFile = Path.Combine(AppContext.BaseDirectory, SettingsFileName);

        public static void Main(string[] args)
        {
            var app = new CommandLineApplication();
            app.Name = "asrs-emulator";
            app.Description = "The local emulator for Azure SignalR Serverless features.";
            app.HelpOption("-h|--help");
            
            app.Command("upstream", command =>
            {
                command.Description = "To init/list the upstream options";
                command.HelpOption("-h|--help");
                command.Command("init", c =>
                {
                    c.Description = "Init the default upstream options into a settings.json config. Use -o to specify the folder to export the default settings.";
                    var configOptions = c.Option("-o|--output", "Specify the folder to init the upstream settings file.", CommandOptionType.SingleValue);
                    c.HelpOption("-h|--help");
                    c.OnExecute(() =>
                    {
                        string outputFile = configOptions.HasValue() ? Path.GetFullPath(Path.Combine(configOptions.Value(), SettingsFileName)) : SettingsFile;
                        if (File.Exists(outputFile))
                        {
                            Console.WriteLine($"Already contains '{outputFile}', still want to override it with the default one? (N/y)");
                            if (Console.ReadKey().Key != ConsoleKey.Y)
                            {
                                return 0;
                            }

                            Console.WriteLine();
                        }

                        Directory.CreateDirectory(Path.GetDirectoryName(outputFile));
                        File.Copy(ProgramDefaultSettingsFile, outputFile, true);

                        Console.WriteLine($"Exported default settings to '{outputFile}'.");
                        return 0;
                    });
                });
                command.Command("list", c =>
                {
                    c.Description = "List current upstream options. Use -c to specify the folder or file to read the settings.";
                    var configOptions = c.Option("-c|--config", "Specify the upstream settings file to load from.", CommandOptionType.SingleValue);
                    c.HelpOption("-h|--help");
                    c.OnExecute(() =>
                    {
                        if (!TryGetConfigFilePath(configOptions, out var config))
                        {
                            return 1;
                        }

                        var host = CreateHostBuilder(args, null, DefaultPort, config).Build();
                        
                        Console.WriteLine($"Loaded upstream settings from '{config}'");
                        
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
                var ipOptions = command.Option("-i|--ip", "Specify the IP address to use.", CommandOptionType.SingleValue);
                var configOptions = command.Option("-c|--config", "Specify the upstream settings file to load from.", CommandOptionType.SingleValue);
                command.HelpOption("-h|--help");
                command.OnExecute(() =>
                {
                    if (!TryGetPort(portOptions, out var port) || !TryGetConfigFilePath(configOptions, out var config) || !TryGetIpAddress(ipOptions, out var ip))
                    {
                        return 1;
                    }

                    Console.WriteLine($"Loaded settings from '{config}'. Changes to the settings file will be hot-loaded into the emulator.");

                    CreateHostBuilder(args, ip, port, config).Build().Run();
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

        public static IHostBuilder CreateHostBuilder(string[] args, IPAddress ip, int port, string configFile)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                    webBuilder.UseKestrel(o =>
                    {
                        if (ip == null)
                        {
                            o.ListenLocalhost(port);
                        }
                        else
                        {
                            o.Listen(ip, port);
                        }
                    });
                })
                .ConfigureAppConfiguration(s =>
                {
                    s.AddJsonFile(AppSettingsFile, optional: true, reloadOnChange: true);
                    s.AddJsonFile(configFile, optional: true, reloadOnChange: true);
                });
        }

        private static bool TryGetIpAddress(CommandOption ipOptions, out IPAddress ip)
        {
            if (ipOptions.HasValue())
            {
                var val = ipOptions.Value();

                if (IPAddress.TryParse(val, out ip))
                {
                    return true;
                }
                else
                {
                    Console.WriteLine($"Invalid IP address value: {val}");
                    return false;
                }
            }
            else
            {
                ip = null;
                return true;
            }
        }

        private static bool TryGetPort(CommandOption portOption, out int port)
        {
            if (portOption.HasValue())
            {
                var val = portOption.Value();

                if( int.TryParse(val, out port))
                {
                    return true;
                }
                else
                {
                    Console.WriteLine($"Invalid port value: {val}");
                    return false;
                }
            }
            else
            {
                port = DefaultPort;
                return true;
            }
        }

        private static bool TryGetConfigFilePath(CommandOption configOption, out string path)
        {
            if (configOption.HasValue())
            {
                var fileAttempt = Path.GetFullPath(configOption.Value());
                if (File.Exists(fileAttempt))
                {
                    path = fileAttempt;
                    return true;
                }

                // Try this as a folder
                var folderAttempt = Path.GetFullPath(Path.Combine(fileAttempt, SettingsFileName));
                if (File.Exists(folderAttempt))
                {
                    path = folderAttempt;
                    return true;
                }

                Console.WriteLine($"Unable to find config file '{fileAttempt}' or '{folderAttempt}'.");
                path = null;
                return false;
            }
            else
            {
                path = SettingsFile;
                return true;
            }
        }
    }
}
