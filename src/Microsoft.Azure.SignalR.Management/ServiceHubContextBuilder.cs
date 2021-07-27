// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
            _hostBuilder.ConfigureServices(services =>
            {
                services.AddHub(hubName);
                services.AddSingleton(services.ToList() as IReadOnlyCollection<ServiceDescriptor>);
            });

            IHost host = null;
            var shouldDispose = false;
            try
            {
                host = _hostBuilder.Build();
                await host.StartAsync(cancellationToken);
                return host.Services.GetRequiredService<ServiceHubContextImpl>();
            }
            catch
            {
                shouldDispose = true;
                if (host != null)
                {
                    await host.StopAsync();
                }
                throw;
            }
            finally
            {
                if(shouldDispose && host!=null)
                {
                    host.Dispose();
                }
            }
        }
    }
}