// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests
{
    public class ServiceEndpointFacts
    {
        [Theory]
        [InlineData("a", "a", EndpointType.Primary)]
        [InlineData("a:primary", "a", EndpointType.Primary)]
        [InlineData("secondary", "secondary", EndpointType.Primary)]
        [InlineData("a:secondary", "a", EndpointType.Secondary)]
        [InlineData(":secondary", "", EndpointType.Secondary)]
        internal void TestParseKey(string key, string expectedName, EndpointType expectedType)
        {
            var (name, type) = ServiceEndpoint.ParseKey(key);

            Assert.Equal(expectedName, name);
            Assert.Equal(expectedType, type);
        }

        [Theory]
        [MemberData(nameof(TestEndpointsEqualityInput))]
        internal void TestEndpointsEquality(ServiceEndpoint first, ServiceEndpoint second, bool equal)
        {
            Assert.Equal(first.Equals(second), equal);
            Assert.Equal(first.GetHashCode() == second.GetHashCode(), equal);
        }

        public static IEnumerable<object[]> TestEndpointsEqualityInput = new object[][]
        {
            new object[]
            {
                new ServiceEndpoint("a", "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0"),
                new ServiceEndpoint("Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0"),
                false,
            },
            new object[]
            {
                new ServiceEndpoint("a", "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0"),
                new ServiceEndpoint(":primary", "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080"),
                false,
            },
            new object[]
            {
                new ServiceEndpoint("a", "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0"),
                new ServiceEndpoint("a:secondary", "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"),
                false,
            },
            new object[]
            {
                new ServiceEndpoint("a", "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0"),
                new ServiceEndpoint("b", "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456780"),
                false,
            },
            new object[]
            {
                new ServiceEndpoint("Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0"),
                new ServiceEndpoint(":secondary", "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456780"),
                false,
            },
            new object[]
            {
                new ServiceEndpoint("Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0", EndpointType.Secondary),
                new ServiceEndpoint("Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456780", name: "Name1"),
                false,
            },
            new object[]
            {
                new ServiceEndpoint("Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0"),
                new ServiceEndpoint("Endpoint=https://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456780"),
                false,
            },
            new object[]
            {
                new ServiceEndpoint("Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0"),
                new ServiceEndpoint("Endpoint=http://localhost;AccessKey=OPQRSTUVWXYZ0123456780ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456780"),
                true,
            },
            new object[]
            {
                new ServiceEndpoint("Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0"),
                new ServiceEndpoint(":primary", "Endpoint=http://localhost;AccessKey=OPQRSTUVWXYZ0123456780ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456780"),
                true,
            },
            new object[]
            {
                new ServiceEndpoint(":secondary", "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0"),
                new ServiceEndpoint("Endpoint=http://localhost;AccessKey=OPQRSTUVWXYZ0123456780ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456780", EndpointType.Secondary),
                true,
            }
        };
    }
}