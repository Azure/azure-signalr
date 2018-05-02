// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.SignalR
{
    public class ServiceContext : IDisposable
    {
        private readonly string _hubName;
        private readonly ServiceProvider _serviceProvider;
        private readonly IServiceEndpointUtility _endpointUtility;

        internal ServiceContext(string hubName, ServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(hubName))
            {
                throw new ArgumentNullException(nameof(hubName));
            }

            _hubName = hubName;
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _endpointUtility = serviceProvider.GetRequiredService<IServiceEndpointUtility>();
            HubContext = new ServiceHubContext(hubName, serviceProvider.GetRequiredService<IHubMessageSender>());
        }

        public IHubContext<Hub> HubContext { get; }

        public string GetEndpoint()
        {
            return _endpointUtility.GetClientEndpoint(_hubName);
        }

        public string GenerateAccessToken(IEnumerable<Claim> claims = null, TimeSpan? lifetime = null)
        {
            return _endpointUtility.GenerateClientAccessToken(_hubName, claims, lifetime);
        }

        public void Dispose()
        {
            _serviceProvider?.Dispose();
        }
    }
}
