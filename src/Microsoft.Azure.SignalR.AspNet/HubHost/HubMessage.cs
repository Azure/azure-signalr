// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal class HubMessage : AppMessage
    {
        public string HubName { get; }

        public HubMessage(string name, ServiceMessage message, Message rawMessage) : base(message, rawMessage)
        {
            HubName = name;
        }
    }
}
