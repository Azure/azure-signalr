// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.SignalR.Tests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.SignalR.E2ETests.SignalR;

internal class TestStartup(IConfiguration configuration) : IStartup
{
    private readonly IConfiguration _configuration = configuration;

    public void Configure(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseEndpoints(configure => configure.MapHub<TestHub>($"/{nameof(TestHub)}"));
        app.UseMvc();
    }

    public IServiceProvider ConfigureServices(IServiceCollection services)
    {
        var applicationName = _configuration[TestConstants.ApplicationName];
        var connectionString = _configuration[TestConstants.ConnectionString];

        services.AddMvc(option => option.EnableEndpointRouting = false);
        services
            .AddSignalR()
            .AddAzureSignalR(o =>
            {
                o.ConnectionString = connectionString;
                o.ClaimsProvider = context => [new Claim(ClaimTypes.NameIdentifier, context.Request.Query["user"])];
                o.ApplicationName = applicationName;
                o.InitialHubServerConnectionCount = 2;
            });

        return services.BuildServiceProvider();
    }
}