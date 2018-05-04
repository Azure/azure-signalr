// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests
{
    public class ConnectionStringParsingFacts
    {
        [Theory]
        [InlineData("Endpoint=aaa;AccessKey=bbb;")]
        [InlineData("ENDPOINT=aaa/;ACCESSKEY=bbb;")]
        public void ValidConnectionString(string connectionString)
        {
            (var endpoint, var accessKey) = ServiceEndpointUtility.ParseConnectionString(connectionString);

            Assert.Equal("aaa", endpoint);
            Assert.Equal("bbb", accessKey);
        }

        [Theory]
        [InlineData("Endpoint=xxx")]
        [InlineData("AccessKey=xxx")]
        [InlineData("XXX=yyy")]
        [InlineData("XXX")]
        public void InvalidConnectionStrings(string connectionString)
        {
            Exception exception = null;
            try
            {
                ServiceEndpointUtility.ParseConnectionString(connectionString);
            }
            catch (ArgumentException ex)
            {
                exception = ex;
            }

            Assert.NotNull(exception);
            Assert.Contains("Connection string missing required properties", exception.Message);
        }
    }
}
