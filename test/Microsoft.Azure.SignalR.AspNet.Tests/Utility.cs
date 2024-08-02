// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.AspNet.Tests;

internal static class Utility
{
    public static HubConfiguration GetTestHubConfig(ILoggerFactory loggerFactory, params string[] hubs)
    {
        var hubConfig = GetActualHubConfig(loggerFactory);
        var testHub = new TestHubManager(hubs);
        hubConfig.Resolver.Register(typeof(IHubManager), () => testHub);
        return hubConfig;
    }

    public static HubConfiguration GetActualHubConfig(ILoggerFactory loggerFactory)
    {
        var resolver = new DefaultDependencyResolver();
        resolver.Register(typeof(ILoggerFactory), () => loggerFactory);
        var hubConfig = new HubConfiguration
        {
            // Resolver is shared in GloblHost, use a new one instead
            Resolver = resolver
        };

        return hubConfig;
    }
}