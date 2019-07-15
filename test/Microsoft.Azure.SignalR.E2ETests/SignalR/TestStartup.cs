// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.SignalR.Tests
{
    internal class TestStartup : IStartup
    {
        private readonly ParameterDelegator _parameterDelegator;

        public TestStartup(ParameterDelegator parameterDelegator)
        {
            _parameterDelegator = parameterDelegator;
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
            var applicationName = _parameterDelegator?.Parameter[ParameterDelegator.ApplicationName] as string;

            services.AddMvc(option => option.EnableEndpointRouting = false);
            services
                .AddSignalR(options =>
                {
                    options.EnableDetailedErrors = true;
                })
                .AddAzureSignalR(o =>
                {
                    o.ConnectionString = TestConfiguration.Instance.ConnectionString;
                    o.ClaimsProvider = context => new[] { new Claim(ClaimTypes.NameIdentifier, context.Request.Query["user"]) };
                    o.ApplicationName = applicationName;
                });

            return services.BuildServiceProvider();
        }
    }
}