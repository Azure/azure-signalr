using Microsoft.AspNet.SignalR.Transports;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    public class TestTransport : IServiceTransport
    {
        public long MessageCount = 0;
        public Func<string, Task> Received { get; set; }
        public Func<Task> Connected { get; set; }
        public Func<Task> Reconnected { get; set; }
        public Func<bool, Task> Disconnected { get; set; }
        public string ConnectionId { get; set; }

        public Task<string> GetGroupsToken()
        {
            return Task.FromResult<string>(null);
        }

        public void OnDisconnected()
        {
        }

        public void OnReceived(string value)
        {
            // Only use to validate message count
            MessageCount++;
        }

        public Task ProcessRequest(ITransportConnection connection)
        {
            return Task.CompletedTask;
        }

        public Task Send(object value)
        {
            return Task.CompletedTask;
        }
    }
}
