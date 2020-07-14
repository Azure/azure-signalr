// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
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
                MessageWithTracingIdHelper.Prefix = (ulong)(Guid.NewGuid().GetHashCode() & 0x0FFF_FFFF) << 32;

                var id1 = MessageWithTracingIdHelper.Generate(true);
                var id2 = MessageWithTracingIdHelper.Generate(false);

                Assert.Equal((id1 & 0x0FFF_FFFF_FFFF_FFFF) + 1, id2 & 0x0FFF_FFFF_FFFF_FFFF);
                Assert.Equal(id1 & 0x1000_0000_0000_0000, (ulong)0x1000_0000_0000_0000);
                Assert.Equal(id2 & 0x1000_0000_0000_0000, (ulong)0);
            }
        }

        [Fact]
        public void TestAssginMessageId()
        {
            var msg1 = new BroadcastDataMessage(null).WithTracingId();

            Assert.NotNull(msg1 as IMessageWithTracingId);
            Assert.Null((msg1 as IMessageWithTracingId).TracingId);

            using (new ClientConnectionScope(null, true))
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