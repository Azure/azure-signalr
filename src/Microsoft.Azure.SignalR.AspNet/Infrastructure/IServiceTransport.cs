// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNet.SignalR.Transports;
using Microsoft.Azure.SignalR.Protocol;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.AspNet
{
    interface IServiceTransport : ITransport
    {
        void OnReceived(string value);
        void OnDisconnected();
        Channel<ServiceMessage> Channel { get; set; }
    }
}
