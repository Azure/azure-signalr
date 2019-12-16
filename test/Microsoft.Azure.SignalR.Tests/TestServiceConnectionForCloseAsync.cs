using System;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;

namespace Microsoft.Azure.SignalR.Tests
{
    class TestServiceConnectionForCloseAsync : TestServiceConnection
    {
        public TestServiceConnectionForCloseAsync() : base(ServiceConnectionStatus.Connected, false)
        {
        }

        /**
         * Register an outgoing Task.
         */
        protected override Task OnClientConnectedAsync(OpenConnectionMessage openConnectionMessage)
        {
            return Task.CompletedTask;
        }
    }
}
