// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.SignalR.Protocol;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests
{
    public class ServiceMessageTracingIdTests
    {
        [Fact]
        public void TestMessageIdGenerator()
        {
            for (var i = 0; i < 100; i++)
            {
                var id1 = MessageWithTracingIdHelper.Generate();
                var id2 = MessageWithTracingIdHelper.Generate();
                Assert.Equal(id1 + 1, id2);
            }
        }

        [Fact]
        public void TestAssginMessageId()
        {
            var msg1 = new BroadcastDataMessage(null).WithTracingId();

            Assert.NotNull(msg1 as IMessageWithTracingId);
            Assert.Null((msg1 as IMessageWithTracingId).TracingId);

            using (new ClientConnectionScope(null, null, true))
            {
                var msg2 = new BroadcastDataMessage(null).WithTracingId();

                Assert.NotNull(msg2 as IMessageWithTracingId);
                Assert.NotNull((msg2 as IMessageWithTracingId).TracingId);
            }

            using (new ServiceConnectionContainerScope(new ServiceDiagnosticLogsContext { EnableMessageLog = true }))
            {
                var msg3 = new BroadcastDataMessage(null).WithTracingId();

                Assert.NotNull(msg3 as IMessageWithTracingId);
                Assert.NotNull((msg3 as IMessageWithTracingId).TracingId);
            }
        }
    }
}