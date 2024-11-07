// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.IntegrationTests.MockService;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
namespace Microsoft.Azure.SignalR.IntegrationTests.Infrastructure
{
    internal class HotReloadIntegrationTestStartup<TParams, THub> : IStartup where THub : Hub
        where TParams : IHotReloadIntegrationTestStartupParameters, new()
    {
        public const string ApplicationName = "AppName";
        private readonly IConfiguration _configuration;
        public HotReloadIntegrationTestStartup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [Obsolete]
        public void Configure(IApplicationBuilder app)
        {
            app.UseAzureSignalR(configure => { configure.MapHub<THub>($"/{nameof(THub)}"); });
            app.UseMvc();
        }

        public static IConfigurationRoot s_config = null;

        public static void ReloadConfig(int index)
        {
            var parameters = new TParams();
            foreach (var s in s_config.Providers)
            {
                if (s is ReloadableMemoryConfigurationProvider prov)
                {
                    prov.Clear();
                    prov.LoadNewData(parameters.Endpoints(index));
                }
            }

            s_config.Reload();
        }

        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            var p = new TParams();
            var applicationName = _configuration[ApplicationName];
            var config = new ConfigurationBuilder()
                .Add(new ReloadableMemoryConfigurationSource { InitialData = p.Endpoints(0) })
                .Build();

            s_config = config;

            services.AddSingleton<IConfiguration>(config);
            services.AddMvc(option => option.EnableEndpointRouting = false);
            services.AddSignalR(options =>
            {
                options.EnableDetailedErrors = true;
            })
                .AddAzureSignalR(o =>
                {
                    o.InitialHubServerConnectionCount = p.ConnectionCount;
                    o.GracefulShutdown.Mode = p.ShutdownMode;
                    //todo: move to params
                    o.ServiceScaleTimeout = TimeSpan.FromSeconds(3);
                    o.ClaimsProvider = context => new[] { new Claim(ClaimTypes.NameIdentifier, context.Request.Query["user"]) };  // todo: migrate to TParams
                    o.ApplicationName = applicationName;
                });

            // Here we inject MockServiceHubDispatcher and use it as a gateway to the MockService side
            services.AddSingleton<IMockService, ConnectionTrackingMockService>();
            services.AddSingleton<IConnectionFactory, MockServiceConnectionContextFactory>();
            services.Replace(ServiceDescriptor.Singleton(typeof(ServiceHubDispatcher<>), typeof(MockServiceHubDispatcher<>)));

            return services.BuildServiceProvider();
        }

        // Custom in memory config provider capable of dynamically changing in memory config data
        internal class ReloadableMemoryConfigurationProvider : ConfigurationProvider, IEnumerable<KeyValuePair<string, string>>
        {
            private readonly ReloadableMemoryConfigurationSource _source;

            public ReloadableMemoryConfigurationProvider(ReloadableMemoryConfigurationSource source)
            {
                _source = source;

                if (_source.InitialData != null)
                {
                    foreach (KeyValuePair<string, string> pair in _source.InitialData)
                    {
                        Data.Add(pair.Key, pair.Value);
                    }
                }
            }

            public void LoadNewData(IEnumerable<KeyValuePair<string, string>> newData)
            {
                foreach (var kv in newData)
                {
                    Data.Add(kv);
                }
            }

            public void Clear() => Data.Clear();
            public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => Data.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        internal class ReloadableMemoryConfigurationSource : IConfigurationSource
        {
            public IEnumerable<KeyValuePair<string, string>> InitialData { get; set; }

            public IConfigurationProvider Build(IConfigurationBuilder builder)
            {
                return new ReloadableMemoryConfigurationProvider(this);
            }
        }
    }
}
