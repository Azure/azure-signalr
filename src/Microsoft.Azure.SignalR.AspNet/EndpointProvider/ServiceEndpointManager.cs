// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal class ServiceEndpointManager : ServiceEndpointManagerBase
    {
        private readonly ServiceOptions _options;

        private readonly IServerNameProvider _provider;

        private readonly ILoggerFactory _loggerFactory;

        public ServiceEndpointManager(
            IServerNameProvider provider,
            IAccessKeyManager manager,
            ServiceOptions options,
            ILoggerFactory loggerFactory) :
            base(
                options,
                manager,
                loggerFactory?.CreateLogger<ServiceEndpointManager>()
            )
        {
            _provider = provider;
            _options = options;
            _loggerFactory = loggerFactory;
        }

        public override IServiceEndpointProvider GetEndpointProvider(ServiceEndpoint endpoint)
        {
            if (endpoint == null)
            {
                return null;
            }

            return new ServiceEndpointProvider(_provider, endpoint, _options, _loggerFactory);
        }
    }
}
