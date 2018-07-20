// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hosting;
using Microsoft.AspNet.SignalR.Transports;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal class AzureTransportManager : ITransportManager
    {
        private readonly IDependencyResolver _resolver;

        public AzureTransportManager(IDependencyResolver resolver)
        {
            _resolver = resolver;
        }

        public ITransport GetTransport(HostContext hostContext)
        {
            return new AzureTransport(hostContext, _resolver);
        }

        public bool SupportsTransport(string transportName)
        {
            // This is only called for websockets, and should never be called in this flow
            return false;
        }
    }
}
