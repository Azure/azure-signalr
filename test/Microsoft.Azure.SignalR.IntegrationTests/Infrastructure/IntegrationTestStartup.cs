// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Azure.SignalR.IntegrationTests.Infrastructure
{
    internal interface IIntegrationTestStartupParameters
    { 
        public int ConnectionCount { get; }
        public ServiceEndpoint[] ServiceEndpoints { get; }
        public GracefulShutdownMode ShutdownMode { get; }
    }

    internal class MockServiceMessageOrderTestParams : IIntegrationTestStartupParameters
    {
        public static int ConnectionCount = 2;
        public static GracefulShutdownMode ShutdownMode = GracefulShutdownMode.WaitForClientsClose;
        public static ServiceEndpoint[] ServiceEndpoints = new[] {
            new ServiceEndpoint("Endpoint=http://127.0.0.1;AccessKey=AAAAAAAAAAAAAAAAAAAAAAAAAA0A2A4A6A8A;Version=1.0;Port=8080", type: EndpointType.Primary, name: "primary"),
            new ServiceEndpoint("Endpoint=http://127.0.1.0;AccessKey=BBBBBBBBBBBBBBBBBBBBBBBBBB0B2B4B6B8B;Version=1.0;Port=8080", type: EndpointType.Secondary, name: "secondary1"),
            new ServiceEndpoint("Endpoint=http://127.1.0.0;AccessKey=CCCCCCCCCCCCCCCCCCCCCCCCCCCC2C4C6C8C;Version=1.0;Port=8080", type: EndpointType.Secondary, name: "secondary2")
        };

        int IIntegrationTestStartupParameters.ConnectionCount => ConnectionCount;
        ServiceEndpoint[] IIntegrationTestStartupParameters.ServiceEndpoints => ServiceEndpoints;
        GracefulShutdownMode IIntegrationTestStartupParameters.ShutdownMode => GracefulShutdownMode.WaitForClientsClose;
    }

    internal class IntegrationTestStartup<TParams, THub> : IStartup 
        where TParams: IIntegrationTestStartupParameters, new()
        where THub: Hub
    {
        public const string ApplicationName = "AppName";
        private readonly IConfiguration _configuration;
        public IntegrationTestStartup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseAzureSignalR(configure =>
            {
                configure.MapHub<THub>($"/{nameof(THub)}");
            });
            app.UseMvc();
        }

        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            var applicationName = _configuration[ApplicationName];
            var p = new TParams();

            services.AddMvc(option => option.EnableEndpointRouting = false);
            services.AddSignalR(options =>
                {
                    options.EnableDetailedErrors = true;
                })
                .AddAzureSignalR(o =>
                {
                    o.ConnectionCount = p.ConnectionCount;
                    o.GracefulShutdown.Mode = p.ShutdownMode;
                    o.Endpoints = p.ServiceEndpoints;
                    o.ClaimsProvider = context => new[] { new Claim(ClaimTypes.NameIdentifier, context.Request.Query["user"]) };  // todo: migrate to TParams
                    o.ConnectionString = TestConfiguration.Instance.ConnectionString;
                    o.ApplicationName = applicationName;
                });

            // Here we inject MockServiceHubDispatcher and use it as a gateway to the MockService side
            services.Replace(ServiceDescriptor.Singleton(typeof(ServiceHubDispatcher<>), typeof(MockServiceHubDispatcher<>)));

            return services.BuildServiceProvider();
        }
    }

    // Specialized startup for hot reload config tests
    // Todo: see if we can deduplicate some of this mess
    internal interface IHotReloadableParams : IIntegrationTestStartupParameters
    {
        // rather than having a fixed set of endpoints IHotReloadableParams provides versioned sets
        public KeyValuePair<string, string>[] Endpoints(int versionIndex);
        public int EndpointsCount { get; }
    }

    internal class HotReloadMessageOrderTestParams : IHotReloadableParams
    {
        public static int ConnectionCount = 1;
        public static GracefulShutdownMode ShutdownMode = GracefulShutdownMode.WaitForClientsClose;

        public static KeyValuePair<string, string>[][] AllEndpoints = new[] {
            new[] {
                new KeyValuePair<string, string>("Azure:SignalR:ConnectionString:One:primary", "Endpoint=http://127.0.0.1;AccessKey=AAAAAAAAAAAAAAAAAAAAAAAAAA0A2A4A6A8A;Version=1.0;Port=8080" ),
                new KeyValuePair<string, string>("Azure:SignalR:ConnectionString:Two:primary", "Endpoint=http://127.0.1.0;AccessKey=BBBBBBBBBBBBBBBBBBBBBBBBBB0B2B4B6B8B;Version=1.0;Port=8080"),
                new KeyValuePair<string, string>("Azure:SignalR:ConnectionString:Three:secondary", "Endpoint=http://127.1.0.0;AccessKey=CCCCCCCCCCCCCCCCCCCCCCCCCCCC2C4C6C8C;Version=1.0;Port=8080")
            },
            new[] {
                new KeyValuePair<string, string>("Azure:SignalR:ConnectionString:Four:primary", "Endpoint=http://127.0.0.2;AccessKey=AAAAAAAAAAAAAAAAAAAAAAAAAA0A2A4A6A8A;Version=1.0;Port=8080" ),
                new KeyValuePair<string, string>("Azure:SignalR:ConnectionString:Five:secondary", "Endpoint=http://127.0.2.0;AccessKey=BBBBBBBBBBBBBBBBBBBBBBBBBB0B2B4B6B8B;Version=1.0;Port=8080"),
                new KeyValuePair<string, string>("Azure:SignalR:ConnectionString:Six:secondary", "Endpoint=http://127.2.0.0;AccessKey=CCCCCCCCCCCCCCCCCCCCCCCCCCCC2C4C6C8C;Version=1.0;Port=8080")
            }
        };

        int IIntegrationTestStartupParameters.ConnectionCount => ConnectionCount;
        ServiceEndpoint[] IIntegrationTestStartupParameters.ServiceEndpoints => new ServiceEndpoint[] { };
        GracefulShutdownMode IIntegrationTestStartupParameters.ShutdownMode => ShutdownMode;
        public KeyValuePair<string, string>[] Endpoints(int versionIndex) => AllEndpoints[versionIndex];
        public int EndpointsCount => AllEndpoints.Length;
    }

    internal class HotReloadIntegrationTestStartup<TParams, THub> : IStartup where THub : Hub
        where TParams : IHotReloadableParams, new()
    {
        public const string ApplicationName = "AppName";
        private readonly IConfiguration _configuration;
        public HotReloadIntegrationTestStartup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseAzureSignalR(configure => { configure.MapHub<THub>($"/{nameof(THub)}"); });
            app.UseMvc();
        }

        public static IConfigurationRoot s_config = null;

        public static void ReloadConfig(int index)
        {
            TParams parameters = new TParams();
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
            TParams p = new TParams();
            var applicationName = _configuration[ApplicationName];
            var config = new ConfigurationBuilder()
            .AddReloadableInMemoryCollection(p.Endpoints(0))    // 0 as the index for initial data
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
                    o.ConnectionCount = p.ConnectionCount;
                    o.GracefulShutdown.Mode = p.ShutdownMode;
                    //todo: move to params
                    o.ServiceScaleTimeout = TimeSpan.FromSeconds(3);
                    //o.Endpoints = p.ServiceEndpoints; 
                    o.ClaimsProvider = context => new[] { new Claim(ClaimTypes.NameIdentifier, context.Request.Query["user"]) };  // todo: migrate to TParams
                    o.ConnectionString = TestConfiguration.Instance.ConnectionString;
                    o.ApplicationName = applicationName;
                });

            // Here we inject MockServiceHubDispatcher and use it as a gateway to the MockService side
            services.Replace(ServiceDescriptor.Singleton(typeof(ServiceHubDispatcher<>), typeof(MockServiceHubDispatcher<>)));

            return services.BuildServiceProvider();
        }
    }

    // Custom config provider based on MemoryConfigurationProvider
    // Able to clear all config data 
    public static class ReloadableMemoryConfigurationBuilderExtensions
    {
        public static IConfigurationBuilder AddReloadableInMemoryCollection(
            this IConfigurationBuilder configurationBuilder,
            IEnumerable<KeyValuePair<string, string>> initialData)
        {
            if (configurationBuilder == null)
            {
                throw new ArgumentNullException(nameof(configurationBuilder));
            }

            configurationBuilder.Add(new ReloadableMemoryConfigurationSource { InitialData = initialData });
            return configurationBuilder;
        }
    }

    public class ReloadableMemoryConfigurationSource : IConfigurationSource
    {
        public IEnumerable<KeyValuePair<string, string>> InitialData { get; set; }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return new ReloadableMemoryConfigurationProvider(this);
        }
    }

    public class ReloadableMemoryConfigurationProvider : ConfigurationProvider, IEnumerable<KeyValuePair<string, string>>
    {
        private readonly ReloadableMemoryConfigurationSource _source;

        public ReloadableMemoryConfigurationProvider(ReloadableMemoryConfigurationSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            _source = source;

            if (_source.InitialData != null)
            {
                foreach (KeyValuePair<string, string> pair in _source.InitialData)
                {
                    Data.Add(pair.Key, pair.Value);
                }
            }
        }

        public void Clear()
        {
            Data.Clear();
        }

        public void LoadNewData(IEnumerable<KeyValuePair<string,string>> newData)
        {
            foreach (var kv in newData)
            {
                Data.Add(kv);
            }
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            return Data.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}