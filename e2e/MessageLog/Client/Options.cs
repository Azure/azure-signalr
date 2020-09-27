using CommandLine;

namespace Microsoft.Azure.SignalR.E2ETest
{
    class Options
    {
        [Option('c', "ConnectionCount", Default = 1)]
        public int ConnectionCount { get; set; }
    
        [Option('u', "Url", Default = "http://localhost:5000/E2ETestHub")]
        public string Url { get; set; }

        [Option('s', "Scenario", Default = Scenario.Echo)]
        public Scenario Scenario { get; set; }

        [Option('p', "Protocol", Default = HubProtocol.Json)]
        public HubProtocol Protocol { get; set; }

        [Option('r', "RepeatSendingTimes", Default = 1)]
        public int RepeatSendingTimes { get; set; }

        [Option('R', "RepeatConnectionTimes", Default = 1)]
        public int RepeatConnectionTimes { get; set; }

        [Option('a', "AspNet", Default = false)]
        public bool UseAspNet { get; set; }
    }
}
