// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;

namespace Microsoft.Azure.SignalR.Tests.Common
{
    public static class FakeEndpointConstant
    {
        public const string FakeAccessKey = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789ABCDEFGH";
        public static readonly string[] FakeEndpointUrls = new string[] { "http://a", "http://b", "http://c" };
        public static readonly string[] FakeConnectionStrings = FakeEndpointUrls.Select(url => $"Endpoint={url};AccessKey={FakeAccessKey};Version=1.0;").ToArray();
        public static readonly ServiceEndpoint[] FakeServiceEndpoints = FakeConnectionStrings.Select(connectionString => new ServiceEndpoint(connectionString)).ToArray();
    }
}
