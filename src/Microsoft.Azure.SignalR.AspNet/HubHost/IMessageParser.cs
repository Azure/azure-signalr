// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.AspNet.SignalR.Messaging;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal interface IMessageParser
    {
        IEnumerable<AppMessage> GetMessages(Message message);
    }
}
