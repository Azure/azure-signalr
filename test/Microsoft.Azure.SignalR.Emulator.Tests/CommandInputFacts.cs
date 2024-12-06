// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Emulator.Tests;

[CollectionDefinition("Console Tests", DisableParallelization = true)]
public class ConsoleTestsCollection
{
    // Empty marker class
}

[Collection("Console Tests")]
public class CommandInputFacts : IDisposable
{
    private StringWriter _writer = new();
    private readonly ITestOutputHelper _output;

    private const string HelpInfo = @"
Description:
  The local emulator for Azure SignalR Serverless features.

Usage:
  asrs-emulator [command] [options]

Options:
  --version       Show version information
  -?, -h, --help  Show help and usage information

Commands:
  upstream  To init/list the upstream options
  start     To start the emulator.
";
    private const string StartHelpInfo = @"
Description:
  To start the emulator.

Usage:
  asrs-emulator start [options]

Options:
  -p, --port <port>      Specify the port to use. [default: 8888]
  -i, --ip <ip>          Specify the IP address to use.
  -c, --config <config>  Specify the upstream settings file to load from.
  -?, -h, --help         Show help and usage information
";
    private const string UpstreamHelpInfo = @"
Description:
  To init/list the upstream options

Usage:
  asrs-emulator upstream [command] [options]

Options:
  -?, -h, --help  Show help and usage information

Commands:
  init  Init the default upstream options into a settings.json config
  list  List current upstream options
";
    private const string UpstreamInitHelpInfo = @"
Description:
  Init the default upstream options into a settings.json config

Usage:
  asrs-emulator upstream init [options]

Options:
  -o, --output <output>  Specify the folder to init the upstream settings file.
  -?, -h, --help         Show help and usage information

";
    private const string UpstreamListHelpInfo = @"
Description:
  List current upstream options

Usage:
  asrs-emulator upstream list [options]

Options:
  -c, --config <config>  Specify the upstream settings file to load from.
  -?, -h, --help         Show help and usage information
";
    public static IEnumerable<object[]> TestData =
        new List<(string command, string output)>
        {
            ("", HelpInfo),
            ("-h", HelpInfo),
            ("--help", HelpInfo),
            ("start -h", StartHelpInfo),
            ("start --help", StartHelpInfo),
            ("upstream -h", UpstreamHelpInfo),
            ("upstream --help", UpstreamHelpInfo),
            ("upstream init -h", UpstreamInitHelpInfo),
            ("upstream init --help", UpstreamInitHelpInfo),
            ("upstream list -h", UpstreamListHelpInfo),
            ("upstream list --help", UpstreamListHelpInfo),
            ("invalid", HelpInfo),
            ("-a", $@"
'-a' was not matched. Did you mean one of the following?
-h
{HelpInfo}
"),
            ("upstream list", $@"Loaded upstream settings from '{Program.ProgramDefaultSettingsFile}'
Current Upstream Settings:
[0]http://localhost:7071/runtime/webhooks/signalr(event:'*',hub:'*',category:'*')
")
        }.Select(s => new object[] { s.command, s.output });
    public CommandInputFacts(ITestOutputHelper output)
    {
        _output = output;
        Console.SetOut(_writer);
    }

    [Theory]
    [MemberData(nameof(TestData))]
    public async Task CommandTests(string input, string expectedOutput)
    {
        Console.WriteLine(input);
        await Program.Main(GetArgs(input));
        var output = _writer.ToString();
        _output.WriteLine(output);
        Assert.Equal(Normalize(input + expectedOutput), Normalize(output));
    }

    private static string Normalize(string input)
    {
        return new string(input.Where(c => c != '\r' && c != '\n' && c != '\t').ToArray());
    }

    public void Dispose()
    {
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
    }

    private static string[] GetArgs(string input)
    {
        return input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }
}