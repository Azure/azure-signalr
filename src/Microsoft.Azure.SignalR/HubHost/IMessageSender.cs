// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Internal.Protocol;

namespace Microsoft.Azure.SignalR
{
    public interface IMessageSender
    {
        Task SendAllProtocolRawMessage(IDictionary<string, string> meta, string method, object[] args);

        Task SendHubMessage(HubMessage message);

        InvocationMessage CreateInvocationMessage(string methodName, object[] args);
    }
}
