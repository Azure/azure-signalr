// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

#nullable enable
namespace Microsoft.Azure.SignalR.Emulator
{
    public class Program
    {
        private const int DefaultPort = 8888;
        private static readonly string SettingsFileName = "settings.json";
        private static readonly string SettingsFile = Path.GetFullPath(SettingsFileName);
        private static readonly string AppSettingsFile = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        internal static readonly string ProgramDefaultSettingsFile = Path.Combine(AppContext.BaseDirectory, SettingsFileName);

        public static async Task<int> Main(string[] args)
        {
            var rootCommand = new RootCommand("The local emulator for Azure SignalR Serverless features.")
            {
                Name = "asrs-emulator"
            };

            rootCommand.AddCommand(CreateUpstreamCommand());
            rootCommand.AddCommand(CreateStartCommand());

            return await rootCommand.InvokeAsync(args);
        }

        private static Command CreateUpstreamCommand()
        {
            var upstreamCommand = new Command("upstream", "To init/list the upstream options")
            {
                CreateInitCommand(),
                CreateListCommand()
            };

            return upstreamCommand;
        }

        private static Command CreateInitCommand()
        {
            var outputOption = new Option<string?>(
                new[] { "-o", "--output" },
                "Specify the folder to init the upstream settings file."
            );

            var initCommand = new Command("init", "Init the default upstream options into a settings.json config")
            {
                outputOption
            };

            initCommand.SetHandler((string? output) =>
            {
                string outputFile = !string.IsNullOrEmpty(output)
                    ? Path.GetFullPath(Path.Combine(output, SettingsFileName))
                    : SettingsFile;

                if (File.Exists(outputFile))
                {
                    Console.WriteLine($"Already contains '{outputFile}', still want to override it with the default one? (N/y)");
                    if (Console.ReadKey().Key != ConsoleKey.Y)
                    {
                        return;
                    }

                    Console.WriteLine();
                }

                Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
                File.Copy(ProgramDefaultSettingsFile, outputFile, true);

                Console.WriteLine($"Exported default settings to '{outputFile}'.");
            }, outputOption);

            return initCommand;
        }

        private static Command CreateListCommand()
        {
            var configOption = new Option<string?>(
                new[] { "-c", "--config" },
                "Specify the upstream settings file to load from."
            );

            var listCommand = new Command("list", "List current upstream options")
            {
                configOption
            };

            listCommand.SetHandler((string? config) =>
            {
                if (!TryGetConfigFilePath(config, out var configFile))
                {
                    return;
                }

                var host = CreateHostBuilder(null, null, DefaultPort, configFile).Build();

                Console.WriteLine($"Loaded upstream settings from '{configFile}'");

                var options = host.Services.GetRequiredService<IOptions<UpstreamOptions>>();
                options.Value.Print();
            }, configOption);

            return listCommand;
        }

        private static Command CreateStartCommand()
        {
            var portOption = new Option<int?>(
                new[] { "-p", "--port" },
                () => DefaultPort,
                "Specify the port to use."
            );
            var ipOption = new Option<string?>(
                new[] { "-i", "--ip" },
                "Specify the IP address to use."
            );
            var configOption = new Option<string?>(
                new[] { "-c", "--config" },
                "Specify the upstream settings file to load from."
            );

            var startCommand = new Command("start", "To start the emulator.")
            {
                portOption,
                ipOption,
                configOption
            };

            startCommand.SetHandler((int? port, string? ip, string? config) =>
            {
                if (!TryGetPort(port, out var actualPort) ||
                    !TryGetConfigFilePath(config, out var configFile) ||
                    !TryGetIpAddress(ip, out var actualIp))
                {
                    return;
                }

                Console.WriteLine($"Loaded settings from '{configFile}'. Changes to the settings file will be hot-loaded into the emulator.");

                CreateHostBuilder(null, actualIp, actualPort, configFile).Build().Run();
            }, portOption, ipOption, configOption);

            return startCommand;
        }

        private static IHostBuilder CreateHostBuilder(string[]? args, IPAddress? ip, int port, string configFile)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                    webBuilder.UseKestrel(options =>
                    {
                        if (ip == null)
                        {
                            options.ListenLocalhost(port);
                        }
                        else
                        {
                            options.Listen(ip, port);
                        }
                    });
                })
                .ConfigureAppConfiguration(config =>
                {
                    config.AddJsonFile(AppSettingsFile, optional: true, reloadOnChange: true);
                    config.AddJsonFile(configFile, optional: true, reloadOnChange: true);
                });
        }

        private static bool TryGetIpAddress(string? ipOption, out IPAddress? ip)
        {
            if (!string.IsNullOrEmpty(ipOption))
            {
                if (IPAddress.TryParse(ipOption, out ip))
                {
                    return true;
                }

                Console.WriteLine($"Invalid IP address value: {ipOption}");
                return false;
            }

            ip = null;
            return true;
        }

        private static bool TryGetPort(int? portOption, out int port)
        {
            port = portOption ?? DefaultPort;
            return true;
        }

        private static bool TryGetConfigFilePath(string? configOption, [NotNullWhen(true)]out string? path)
        {
            if (!string.IsNullOrEmpty(configOption))
            {
                var fileAttempt = Path.GetFullPath(configOption);
                if (File.Exists(fileAttempt))
                {
                    path = fileAttempt;
                    return true;
                }

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

            path = SettingsFile;
            return true;
        }
    }
}
