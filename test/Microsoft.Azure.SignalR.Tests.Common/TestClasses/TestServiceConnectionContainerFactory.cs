// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.Tests.Common;

internal class TestServiceConnectionContainerFactory : IServiceConnectionContainerFactory
{
    private readonly SortedList<string, ServiceMessage> _messages;

    public TestServiceConnectionContainerFactory(SortedList<string, ServiceMessage> output)
    {
        _messages = output;
    }

    public IServiceConnectionContainer Create(string hub)
    {
        return new TestServiceConnectionContainer(hub,
            m =>
            {
                lock (_messages)
                {
                    _messages.Add(hub, m.Item1);
                }
            });
    }
}