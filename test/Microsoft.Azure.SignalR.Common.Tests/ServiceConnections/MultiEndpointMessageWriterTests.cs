// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Azure;

using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests.ServiceConnections;

public class MultiEndpointMessageWriterTests
{
    [Theory]
    [InlineData(null, 6, 2, null, null)]
    [InlineData(6, 6, 2, 6, 3)]
    [InlineData(3, 3, 1, 3)]
    [InlineData(7, 6, 2, 7, 4)]
    public async Task ListConnectionsInGroup(int? top, int resultCount, int expectedResultPage, params int?[] expectedTopsInInvocations)
    {
        var targetEndpoints = new List<HubServiceEndpoint>();
        var containerMocks = new List<Mock<IServiceConnectionContainer>>();
        for (var i = 0; i < 2; i++)
        {
            var endpoint = new TestHubServiceEndpoint();
            var resultFromConnectioContainer = MockAsyncEnumerable<GroupMember>.From(new GroupMemberQueryResultPage([
                new GroupMember { ConnectionId = "1" },
                new GroupMember { ConnectionId = "2" },
                new GroupMember { ConnectionId = "3" }],
                null)
            );
            var containerMock = new Mock<IServiceConnectionContainer>();
            containerMocks.Add(containerMock);
            containerMock.Setup(c => c.ListConnectionsInGroupAsync(It.IsAny<string>(), It.IsAny<int?>(), null, null, default, default))
                .Returns(resultFromConnectioContainer);
            endpoint.ConnectionContainer = containerMock.Object;
            targetEndpoints.Add(endpoint);
        }
        var multiEndpointWriter = new MultiEndpointMessageWriter(targetEndpoints, Mock.Of<ILoggerFactory>());
        var resultPages = new List<Page<GroupMember>>();
        await foreach (var page in multiEndpointWriter.ListConnectionsInGroupAsync("group", top))
        {
            resultPages.Add(page);
        }
        Assert.Equal(expectedResultPage, resultPages.Count);
        Assert.Equal(resultCount, resultPages.SelectMany(r => r.Values).Count());
        for (var i = 0; i < expectedTopsInInvocations.Length; i++)
        {
            containerMocks[i].Verify(c => c.ListConnectionsInGroupAsync("group", expectedTopsInInvocations[i], null, null, default, default), Times.Once());
        }
    }
}
