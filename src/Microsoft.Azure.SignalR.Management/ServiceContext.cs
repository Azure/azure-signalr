// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;

namespace Microsoft.Azure.SignalR.Management
{
    internal class ServiceContext : IServiceContext
    {
        private readonly ServiceHubContextFactory _serviceHubContextFactory;
        private readonly IServiceProvider _serviceProvider;

        public ServiceContext(ServiceHubContextFactory serviceHubContextFactory, IServiceProvider serviceProvider)
        {
            _serviceHubContextFactory = serviceHubContextFactory;
            _serviceProvider = serviceProvider;
        }

        public Task<IServiceHubContext> CreateHubContextAsync(string hubName, CancellationToken cancellationToken = default)
        {
            return _serviceHubContextFactory.CreateAsync(hubName, null, cancellationToken);
        }

        public void Dispose()
        {
            (_serviceProvider as IDisposable)?.Dispose();
        }
    }
}