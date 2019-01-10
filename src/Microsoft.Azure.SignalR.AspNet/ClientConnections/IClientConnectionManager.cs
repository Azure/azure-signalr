// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal interface IClientConnectionManager
    {
        IServiceTransport CreateConnection(OpenConnectionMessage message, IServiceConnection serviceConnection);
    }
}
