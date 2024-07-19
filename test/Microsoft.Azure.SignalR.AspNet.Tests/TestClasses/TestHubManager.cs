// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNet.SignalR.Hubs;
using Microsoft.AspNet.SignalR.Json;

namespace Microsoft.Azure.SignalR.AspNet.Tests;

internal sealed class TestHubManager : IHubManager
{
    private readonly string[] _hubs;
    public TestHubManager(params string[] hubs)
    {
        _hubs = hubs;
    }

    public HubDescriptor GetHub(string hubName)
    {
        return null;
    }

    public MethodDescriptor GetHubMethod(string hubName, string method, IList<IJsonValue> parameters)
    {
        return null;
    }

    public IEnumerable<MethodDescriptor> GetHubMethods(string hubName, Func<MethodDescriptor, bool> predicate)
    {
        yield break;
    }

    public IEnumerable<HubDescriptor> GetHubs(Func<HubDescriptor, bool> predicate)
    {
        return _hubs.Select(s => new HubDescriptor() { Name = s });
    }

    public IHub ResolveHub(string hubName)
    {
        return null;
    }

    public IEnumerable<IHub> ResolveHubs()
    {
        yield break;
    }
}