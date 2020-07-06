// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Security.Claims;

namespace Microsoft.Azure.SignalR.IntegrationTests.Infrastructure
{
    internal class IntegrationTestStartup : IStartup
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
                configure.MapHub<TestHub>($"/{nameof(TestHub)}");
            });
            app.UseMvc();
        }

        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            var applicationName = _configuration[ApplicationName];

            services.AddMvc(option => option.EnableEndpointRouting = false);
            services.AddSignalR(options =>
                {
                    options.EnableDetailedErrors = true;
                })
                .AddAzureSignalR(o =>
                {
                    o.ConnectionCount = 1;
                    o.GracefulShutdown.Mode = GracefulShutdownMode.WaitForClientsClose;
                    o.ConnectionString = TestConfiguration.Instance.ConnectionString;
                    o.ClaimsProvider = context => new[] { new Claim(ClaimTypes.NameIdentifier, context.Request.Query["user"]) };
                    o.ApplicationName = applicationName;
                    o.Endpoints = new[] { new ServiceEndpoint("Endpoint=http://127.0.0.1;AccessKey=AAAAAAAAAAAAAAAAAAAAAAAAAA0A2A4A6A8A;Version=1.0;Port=8080") };
                });

            //
            // inject MockServiceHubDispatcher and use it as a gateway to the MockService side
            //
            services.Replace(ServiceDescriptor.Singleton(typeof(ServiceHubDispatcher<>), typeof(MockServiceHubDispatcher<>)));

            return services.BuildServiceProvider();
        }
    }
}