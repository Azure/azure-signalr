// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

Usage: asrs-emulator [options] [command]

Options:
  -h|--help  Show help information

Commands:
  start     To start the emulator.
  upstream  To init/list the upstream options

Use ""asrs-emulator [command] --help"" for more information about a command.

";
    private const string StartHelpInfo = @"

Usage: asrs-emulator start [options]

Options:
  -p|--port    Specify the port to use.
  -i|--ip      Specify the IP address to use.
  -c|--config  Specify the upstream settings file to load from.
  -h|--help    Show help information

";
    private const string UpstreamHelpInfo = @"

Usage: asrs-emulator upstream [options] [command]

Options:
  -h|--help  Show help information

Commands:
  init  Init the default upstream options into a settings.json config. Use -o to specify the folder to export the default settings.
  list  List current upstream options. Use -c to specify the folder or file to read the settings.

Use ""upstream [command] --help"" for more information about a command.

";
    private const string UpstreamInitHelpInfo = @"

Usage: asrs-emulator upstream init [options]

Options:
  -o|--output  Specify the folder to init the upstream settings file.
  -h|--help    Show help information

";
    private const string UpstreamListHelpInfo = @"

Usage: asrs-emulator upstream list [options]

Options:
  -c|--config  Specify the upstream settings file to load from.
  -h|--help    Show help information

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
            ("invalid", @"Specify --help for a list of available options and commands.
Error starting emulator: Unrecognized command or argument 'invalid'.
"),
            ("-a", @"Specify --help for a list of available options and commands.
Error starting emulator: Unrecognized option '-a'.
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
    public void CommandTests(string input, string expectedOutput)
    {
        Console.WriteLine(input);
        Program.Main(GetArgs(input));
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