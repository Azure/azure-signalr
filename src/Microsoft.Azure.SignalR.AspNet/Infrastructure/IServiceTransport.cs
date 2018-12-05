// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNet.SignalR.Transports;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal interface IServiceTransport : ITransport
    {
        void OnReceived(string value);

        void OnDisconnected();
    }
}
