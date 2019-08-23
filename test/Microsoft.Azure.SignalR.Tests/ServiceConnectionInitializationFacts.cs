using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Tests.Common;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests
{
    [Collection("ServiceConnectionInitialization")]
    public class ServiceConnectionInitializationFacts
    {
        /// <summary>
        /// Test if there's a deadlock in server connection initialization. _serviceConnectionStartTcs in ServiceConnectionBase should be inited with option TaskCreationOptions.RunContinuationsAsynchronously
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task ServiceConnectionInitializationDeadlockTest()
        {
            var context = SynchronizationContext.Current;
            try
            {
                SynchronizationContext.SetSynchronizationContext(null);
                var conn = new TestServiceConnection();
                var initTask = conn.StartAsync();
                await conn.ConnectionInitializedTask;
                conn.Stop();
                var completedTask = Task.WhenAny(initTask, Task.Delay(TimeSpan.FromSeconds(1))).Result;
                Assert.Equal(initTask, completedTask);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(context);
            }
        }
    }
}
