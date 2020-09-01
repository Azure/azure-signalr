// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Security.Claims;

namespace Microsoft.Azure.SignalR.IntegrationTests.Infrastructure
{
    internal interface IIntegrationTestStartupParameters
    { 
        public int ConnectionCount { get; }
        public ServiceEndpoint[] ServiceEndpoints { get; }
        public GracefulShutdownMode ShutdownMode { get; }
    }

    internal class RealMockServiceE2ETestParams : IIntegrationTestStartupParameters
    {
        public static int ConnectionCount = 2;
        public static GracefulShutdownMode ShutdownMode = GracefulShutdownMode.WaitForClientsClose;
        public static ServiceEndpoint[] ServiceEndpoints = new[] {
            new ServiceEndpoint("Endpoint=http://127.0.0.1;AccessKey=AAAAAAAAAAAAAAAAAAAAAAAAAA0A2A4A6A8A;Version=1.0;Port=8080", type: EndpointType.Primary, name: "primary"),
            new ServiceEndpoint("Endpoint=http://127.1.1.0;AccessKey=BBBBBBBBBBBBBBBBBBBBBBBBBB0B2B4B6B8B;Version=1.0;Port=8080", type: EndpointType.Secondary, name: "secondary")
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
}