using System;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests
{
    public class MessageIdHelperTests
    {
        [Fact]
        public void TestMessageIdGenerator()
        {
            var id1 = Convert.ToUInt64(MessageIdHelper.Generate(true));
            var id2 = Convert.ToUInt64(MessageIdHelper.Generate(false));

            Assert.Equal((id1 & 0x0FFF_FFFF_FFFF_FFFF) + 1, id2 & 0x0FFF_FFFF_FFFF_FFFF);
            Assert.Equal(id1 & 0x1000_0000_0000_0000, (ulong)0x1000_0000_0000_0000);
            Assert.Equal(id2 & 0x1000_0000_0000_0000, (ulong)0);
        }
    }
}
