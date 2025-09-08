// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests
{
    public class GroupMemberQueryResultPageTests
    {
        [Fact]
        public void CanDeserializeGroupMemberQueryResultPage()
        {
            // Arrange
            var json = @"
            {
                ""value"": [
                    { ""connectionId"": ""conn1"", ""userId"": ""user1"" },
                    { ""connectionId"": ""conn2"", ""userId"": null }
                ],
                ""nextLink"": ""token123""
            }";

            // Act
            var result = JsonSerializer.Deserialize<GroupMemberQueryResultPage>(json);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Values);
            Assert.Equal(2, result.Values.Count);
            Assert.Equal("conn1", result.Values[0].ConnectionId);
            Assert.Equal("user1", result.Values[0].UserId);
            Assert.Equal("conn2", result.Values[1].ConnectionId);
            Assert.Null(result.Values[1].UserId);
            Assert.Equal("token123", result.ContinuationToken);
        }
    }
}
