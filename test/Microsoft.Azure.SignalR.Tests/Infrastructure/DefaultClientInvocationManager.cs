// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Internal;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Azure.SignalR
{
    internal class DefaultClientInvocationManager : IClientInvocationManager
    {
        public ICallerClientResultsManager Caller { get; }
        public IRoutedClientResultsManager Router { get; }

        public DefaultClientInvocationManager()
        {
            var hubProtocolResolver = new DefaultHubProtocolResolver(
                    new IHubProtocol[] { 
                        new JsonHubProtocol(), 
                        new MessagePackHubProtocol() 
                    },
                    NullLogger<DefaultHubProtocolResolver>.Instance);

            Caller = new CallerClientResultsManager(hubProtocolResolver);
            Router = new RoutedClientResultsManager();
        }

        public bool TryGetInvocationReturnType(string invocationId, out Type type)
        {
            if (Router.TryGetInvocationReturnType(invocationId, out type))
            {
                return true;
            }
            return Caller.TryGetInvocationReturnType(invocationId, out type);
        }

        public void CleanupInvocationsByConnection(string connectionId)
        {
            Caller.CleanupInvocationsByConnection(connectionId);
            Router.CleanupInvocationsByConnection(connectionId);
        }
    }
}
