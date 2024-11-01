// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
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
            app.UseRouting();
            app.UseEndpoints(configure =>
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
                    o.InitialHubServerConnectionCount = p.ConnectionCount;
                    o.GracefulShutdown.Mode = p.ShutdownMode;
                    o.Endpoints = p.ServiceEndpoints;
                    o.ClaimsProvider = context => new[] { new Claim(ClaimTypes.NameIdentifier, context.Request.Query["user"]) };  // todo: migrate to TParams
                    o.ApplicationName = applicationName;
                });

            // Here we inject MockServiceHubDispatcher and use it as a gateway to the MockService side
            services.AddSingleton<IMockService, ConnectionTrackingMockService>();
            services.AddSingleton<IConnectionFactory, MockServiceConnectionContextFactory>();
            services.Replace(ServiceDescriptor.Singleton(typeof(ServiceHubDispatcher<>), typeof(MockServiceHubDispatcher<>)));

            return services.BuildServiceProvider();
        }
    }
}