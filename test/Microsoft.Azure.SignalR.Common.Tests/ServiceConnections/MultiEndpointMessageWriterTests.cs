// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests.ServiceConnections;
public class MultiEndpointMessageWriterTests
{
    [Theory]
    [InlineData(null, 6, null, null)]
    [InlineData(5, 5, 5, 2)]
    [InlineData(1, 1, 1)]
    [InlineData(7, 6, 7, 4)]
    public async Task ListConnectionsInGroup(int? top, int resultCount, params int?[] expectedTopsInInvocations)
    {
        var targetEndpoints = new List<HubServiceEndpoint>();
        var containerMocks = new List<Mock<IServiceConnectionContainer>>();
        for (var i = 0; i < 2; i++)
        {
            var endpoint = new TestHubServiceEndpoint();
            var resultFromConnectioContainer = MockAsyncEnumerable<GroupMember>.From(
                new GroupMember { ConnectionId = "1" },
                new GroupMember { ConnectionId = "2" },
                new GroupMember { ConnectionId = "3" }
            );
            var containerMock = new Mock<IServiceConnectionContainer>();
            containerMocks.Add(containerMock);
            containerMock.Setup(c => c.ListConnectionsInGroupAsync(It.IsAny<string>(), It.IsAny<int?>(), null, default))
                .Returns(resultFromConnectioContainer);
            endpoint.ConnectionContainer = containerMock.Object;
            targetEndpoints.Add(endpoint);
        }
        var multiEndpointWriter = new MultiEndpointMessageWriter(targetEndpoints, Mock.Of<ILoggerFactory>());
        var resultMembers = new List<GroupMember>();
        await foreach (var member in multiEndpointWriter.ListConnectionsInGroupAsync("group", top))
        {
            resultMembers.Add(member);
        }
        Assert.Equal(resultCount, resultMembers.Count);
        for (var i = 0; i < expectedTopsInInvocations.Length; i++)
        {
            containerMocks[i].Verify(c => c.ListConnectionsInGroupAsync("group", expectedTopsInInvocations[i], null, default), Times.Once());
        }
    }
}
