// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR.IntegrationTests.Infrastructure.MessageOrderTests;

internal class MockServiceMessageOrderTestParams : IIntegrationTestStartupParameters
{
    public static int ConnectionCount = 2;

    public static GracefulShutdownMode ShutdownMode = GracefulShutdownMode.WaitForClientsClose;

    public static ServiceEndpoint[] ServiceEndpoints = [
        new ServiceEndpoint("Endpoint=http://127.0.0.1;AccessKey=AAAAAAAAAAAAAAAAAAAAAAAAAA0A2A4A6A8A;Version=1.0;Port=8080", type: EndpointType.Primary, name: "primary"),
        new ServiceEndpoint("Endpoint=http://127.0.1.0;AccessKey=BBBBBBBBBBBBBBBBBBBBBBBBBB0B2B4B6B8B;Version=1.0;Port=8080", type: EndpointType.Secondary, name: "secondary1"),
        new ServiceEndpoint("Endpoint=http://127.1.0.0;AccessKey=CCCCCCCCCCCCCCCCCCCCCCCCCCCC2C4C6C8C;Version=1.0;Port=8080", type: EndpointType.Secondary, name: "secondary2")
    ];

    int IIntegrationTestStartupParameters.ConnectionCount => ConnectionCount;

    ServiceEndpoint[] IIntegrationTestStartupParameters.ServiceEndpoints => ServiceEndpoints;

    GracefulShutdownMode IIntegrationTestStartupParameters.ShutdownMode => GracefulShutdownMode.WaitForClientsClose;
}
