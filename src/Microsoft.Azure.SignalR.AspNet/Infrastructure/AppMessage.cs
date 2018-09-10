// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal class AppMessage
    {
        public ServiceMessage Message { get; }

        public Message RawMessage { get; }

        public AppMessage(ServiceMessage message, Message rawMessage)
        {
            Message = message;
            RawMessage = rawMessage;
        }
    }
}
