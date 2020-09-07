using System.Collections.Concurrent;

namespace Microsoft.Azure.SignalR.E2ETest
{
    public class Data
    {
        public ConcurrentDictionary<string, byte> Connections { get; set; } = new ConcurrentDictionary<string, byte>();
        public string Prefix = null;
    }
}
