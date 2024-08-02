// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.SignalR.Tests.Common;

internal class TestHubServiceEndpoint(string name = null,
                                      IServiceEndpointProvider provider = null,
                                      ServiceEndpoint endpoint = null) : HubServiceEndpoint(
                                          name ?? "foo",
                                          provider ?? new TestServiceEndpointProvider(),
                                          endpoint ?? new TestServiceEndpoint())
{
}