// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.SignalR.Management;

internal class ServiceManagerOptionsSetup(IConfiguration configuration = null) : IConfigureOptions<ServiceManagerOptions>, IOptionsChangeTokenSource<ServiceManagerOptions>
{
    public string Name => Options.DefaultName;

    public void Configure(ServiceManagerOptions options)
    {
        if (configuration != null)
        {
            configuration.GetSection(Constants.Keys.AzureSignalRSectionKey).Bind(options);
            var endpoints = configuration.GetEndpoints(Constants.Keys.AzureSignalREndpointsKey).ToArray();
            if (endpoints.Length > 0)
            {
                options.ServiceEndpoints = endpoints;
            }
        }
    }

    public IChangeToken GetChangeToken()
    {
        return configuration?.GetReloadToken() ?? NullChangeToken.Singleton;
    }
}
