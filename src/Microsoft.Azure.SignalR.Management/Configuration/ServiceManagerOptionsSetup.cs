// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.SignalR.Management
{
    internal class ServiceManagerOptionsSetup : IConfigureOptions<ServiceManagerOptions>, IOptionsChangeTokenSource<ServiceManagerOptions>
    {
        private readonly AzureComponentFactory _azureComponentFactory;
        private readonly IConfiguration _configuration;

        public ServiceManagerOptionsSetup(AzureComponentFactory azureComponentFactory, IConfiguration configuration = null)
        {
            _azureComponentFactory = azureComponentFactory;
            _configuration = configuration;
        }

        public string Name => Options.DefaultName;

        public void Configure(ServiceManagerOptions options)
        {
            if (_configuration != null)
            {
                var section = _configuration.GetSection(Constants.Keys.AzureSignalRSectionKey);
                section.Bind(options);

                //get multiple named endpoints.
                var endpoints = _configuration.GetSection(Constants.Keys.AzureSignalREndpointsKey).GetEndpoints(_azureComponentFactory);
                //try to get the single identity-based nameless endpoint. 
                if (section.GetSection(Constants.Keys.IdentityBasedSingleEndpointKey).TryGetNamedEndpointFromIdentity(_azureComponentFactory, out var singleEndpoint))
                {
                    //reset the name
                    singleEndpoint.Name = string.Empty;
                    endpoints = endpoints.Append(singleEndpoint);
                }
                var endpointArray = endpoints.ToArray();
                if (endpointArray.Length > 0)
                {
                    options.ServiceEndpoints = endpointArray;
                }
            }
        }

        public IChangeToken GetChangeToken()
        {
            return _configuration?.GetReloadToken() ?? NullChangeToken.Singleton;
        }
    }
}