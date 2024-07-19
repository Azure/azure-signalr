// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.Tests.Common;

internal sealed class TestServiceMessageHandler : IServiceMessageHandler
{
    public TestServiceMessageHandler()
    {
    }

    public Task HandlePingAsync(PingMessage pingMessage) => Task.CompletedTask;

    public void HandleAck(AckMessage serviceMessage)
    {
        throw new NotImplementedException();
    }

    public Task HandleKeyAsync(AccessKeyResponseMessage keyMessage) => Task.CompletedTask;
}
