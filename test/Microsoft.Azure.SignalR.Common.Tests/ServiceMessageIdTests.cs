using Microsoft.Azure.SignalR.Protocol;
using System;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests
{
    public class ServiceMessageIdTests
    {
        [Fact]
        public void TestMessageIdGenerator()
        {
            for (var i = 0; i < 100; i++)
            {
                MessageIdHelper.Prefix = Guid.NewGuid().GetHashCode();

                var id1 = Convert.ToUInt64(MessageIdHelper.Generate(true));
                var id2 = Convert.ToUInt64(MessageIdHelper.Generate(false));

                Assert.Equal((id1 & 0x0FFF_FFFF_FFFF_FFFF) + 1, id2 & 0x0FFF_FFFF_FFFF_FFFF);
                Assert.Equal(id1 & 0x1000_0000_0000_0000, (ulong)0x1000_0000_0000_0000);
                Assert.Equal(id2 & 0x1000_0000_0000_0000, (ulong)0);
            }
        }

        [Fact]
        public void TestAssginMessageId()
        {
            var msg1 = new BroadcastDataMessage(null).WithMessageId();

            Assert.NotNull(msg1 as IMessageWithTracingId);
            Assert.False(string.IsNullOrEmpty((msg1 as IMessageWithTracingId).TracingId));
        }
    }
}
