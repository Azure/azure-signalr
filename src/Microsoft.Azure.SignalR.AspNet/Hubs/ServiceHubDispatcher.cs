// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hosting;
using Microsoft.AspNet.SignalR.Hubs;
using Microsoft.AspNet.SignalR.Infrastructure;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal class ServiceHubDispatcher : HubDispatcher
    {
        private static readonly ProtocolResolver ProtocolResolver = new ProtocolResolver();

        private IServiceEndpoint _endpoint;

        public ServiceHubDispatcher(HubConfiguration configuration) : base(configuration)
        {
        }

        public override void Initialize(IDependencyResolver resolver)
        {
            _endpoint = resolver.Resolve<IServiceEndpoint>();
            base.Initialize(resolver);
        }

        public override Task ProcessRequest(HostContext context)
        {
            // Redirect negotiation to service
            if (IsNegotiationRequest(context.Request))
            {
                return ProcessNegotiationRequest(context);
            }

            return base.ProcessRequest(context);
        }

        private Task ProcessNegotiationRequest(HostContext context)
        {
            throw new NotImplementedException();
        }

        private static bool IsNegotiationRequest(IRequest request)
        {
            return request.LocalPath.EndsWith(Constants.Path.Negotiate, StringComparison.OrdinalIgnoreCase);
        }
    }
}
