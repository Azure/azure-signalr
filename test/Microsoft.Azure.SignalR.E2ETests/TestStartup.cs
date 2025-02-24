// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Security.Claims;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.SignalR.E2ETests.Common;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.SignalR.E2ETests;

internal sealed class TestStartup(IConfiguration configuration) : IStartup
{
    public const string ApplicationName = "AppName";

    public const string ConnectionString = "ConnectionString";

    private readonly IConfiguration _configuration = configuration;

    public void Configure(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseEndpoints(configure => configure.MapHub<TestHub>($"/{nameof(TestHub)}"));
        app.UseMvc();
    }

    public IServiceProvider ConfigureServices(IServiceCollection services)
    {
        var applicationName = _configuration[ApplicationName];
        var connectionString = _configuration[ConnectionString] ?? TestConfiguration.Instance.ConnectionString;

        services.AddMvc(option => option.EnableEndpointRouting = false);
        services
            .AddSignalR(options => options.EnableDetailedErrors = true)
            .AddAzureSignalR(o =>
            {
                o.ConnectionString = connectionString;
                o.ClaimsProvider = context => [new Claim(ClaimTypes.NameIdentifier, context.Request.Query["user"].ToString())];
                o.ApplicationName = applicationName;
                // o.GracefulShutdown = new GracefulShutdownOptions()
                // {
                //     Mode = GracefulShutdownMode.MigrateClients,
                // };
            });

        services.AddSingleton<ICustomHeaderProvider, IngressHeaderProvider>();
        return services.BuildServiceProvider();
    }
}
