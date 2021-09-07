// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Azure.SignalR.Management
{
    internal class ServiceHubContextBuilder
    {
        private readonly HostBuilder _hostBuilder = new();

        internal ServiceHubContextBuilder(IEnumerable<ServiceDescriptor> srcServices)
        {
            _hostBuilder.ConfigureServices(collection => collection.Add(srcServices));
        }

        internal ServiceHubContextBuilder ConfigureServices(Action<IServiceCollection> configure)
        {
            _hostBuilder.ConfigureServices(configure);
            return this;
        }

        /// <summary>
        /// Builds <see cref="ServiceHubContext"/> instances.
        /// </summary>
        /// <returns>The instance of the <see cref="IServiceManager"/>.</returns>
        internal async Task<ServiceHubContext> CreateAsync(string hubName, CancellationToken cancellationToken)
        {
            _hostBuilder.ConfigureServices(services => services.AddHub<Hub>(hubName));
            var host = await CreateAndStartHost(cancellationToken);
            return host.Services.GetRequiredService<ServiceHubContext>();
        }

        public async Task<ServiceHubContext<T>> CreateAsync<T>(string hubName, CancellationToken cancellationToken) where T : class
        {
            _hostBuilder.ConfigureServices(services => services.AddHub<Hub<T>, T>(hubName));
            var host = await CreateAndStartHost(cancellationToken);
            return host.Services.GetRequiredService<ServiceHubContext<T>>();
        }

        private async Task<IHost> CreateAndStartHost(CancellationToken cancellationToken)
        {
            _hostBuilder.ConfigureServices(services =>
            {
                services.AddSingleton(services.ToList() as IReadOnlyCollection<ServiceDescriptor>);
            });

            IHost host = null;
            try
            {
                host = _hostBuilder.Build();
                await host.StartAsync(cancellationToken);
                return host;
            }
            catch
            {
                using (host)
                {
                    await host.StopAsync();
                }
                throw;
            }
        }
    }
}