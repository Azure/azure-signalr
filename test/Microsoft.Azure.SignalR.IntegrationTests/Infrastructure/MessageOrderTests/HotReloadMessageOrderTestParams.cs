// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Azure.SignalR.IntegrationTests.Infrastructure.MessageOrderTests
{
    internal class HotReloadMessageOrderTestParams : IHotReloadIntegrationTestStartupParameters
    {
        public static int ConnectionCount = 1;
        public static GracefulShutdownMode ShutdownMode = GracefulShutdownMode.WaitForClientsClose;

        public static KeyValuePair<string, string>[][] AllEndpoints = new[] {
            new[] {
                new KeyValuePair<string, string>("Azure:SignalR:ConnectionString:One:primary", "Endpoint=http://127.0.0.1;AccessKey=AAAAAAAAAAAAAAAAAAAAAAAAAA0A2A4A6A8A;Version=1.0;Port=8080" ),
                new KeyValuePair<string, string>("Azure:SignalR:ConnectionString:Two:primary", "Endpoint=http://127.0.1.0;AccessKey=BBBBBBBBBBBBBBBBBBBBBBBBBB0B2B4B6B8B;Version=1.0;Port=8080"),
                new KeyValuePair<string, string>("Azure:SignalR:ConnectionString:Three:secondary", "Endpoint=http://127.1.0.0;AccessKey=CCCCCCCCCCCCCCCCCCCCCCCCCCCC2C4C6C8C;Version=1.0;Port=8080")
            },
            new[] {
                new KeyValuePair<string, string>("Azure:SignalR:ConnectionString:Four:primary", "Endpoint=http://127.0.0.2;AccessKey=AAAAAAAAAAAAAAAAAAAAAAAAAA0A2A4A6A8A;Version=1.0;Port=8080" ),
                new KeyValuePair<string, string>("Azure:SignalR:ConnectionString:Five:secondary", "Endpoint=http://127.0.2.0;AccessKey=BBBBBBBBBBBBBBBBBBBBBBBBBB0B2B4B6B8B;Version=1.0;Port=8080"),
                new KeyValuePair<string, string>("Azure:SignalR:ConnectionString:Six:secondary", "Endpoint=http://127.2.0.0;AccessKey=CCCCCCCCCCCCCCCCCCCCCCCCCCCC2C4C6C8C;Version=1.0;Port=8080")
            }
        };

        int IIntegrationTestStartupParameters.ConnectionCount => ConnectionCount;
        ServiceEndpoint[] IIntegrationTestStartupParameters.ServiceEndpoints => new ServiceEndpoint[] { };
        GracefulShutdownMode IIntegrationTestStartupParameters.ShutdownMode => ShutdownMode;
        // rather than having a fixed set of endpoints hot reload startup parameters provide versioned sets
        public KeyValuePair<string, string>[] Endpoints(int versionIndex) => AllEndpoints[versionIndex];
        public int EndpointsCount => AllEndpoints.Length;
    }
}
