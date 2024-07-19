// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests;

public class AzureSignalRMarkerServiceFact
{
    private const string DefaultValue = "Endpoint=https://abc;AccessKey=abc123;";

    [Fact]
    [Obsolete]
    public void UseAzureSignalRWithAddAzureSignalR()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                {"Azure:SignalR:ConnectionString", DefaultValue}
            })
            .Build();
        var serviceProvider = services.AddLogging()
            .AddSignalR()
            .AddAzureSignalR()
            .Services
            .AddSingleton<IConfiguration>(config)
            .BuildServiceProvider();

        var app = new ApplicationBuilder(serviceProvider);
        app.UseAzureSignalR(routes =>
        {
            routes.MapHub<TestHub>("/chat");
        });

        Assert.NotNull(serviceProvider.GetService<HubLifetimeManager<TestHub>>());
    }

    [SkipIfTargetNetstandard]
    public void UseEndpointsWithAddAzureSignalR()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                {"Azure:SignalR:ConnectionString", DefaultValue}
            })
            .Build();
        var serviceProvider = services.AddLogging()
            .AddSignalR()
            .AddAzureSignalR()
            .Services
            .AddSingleton<IHostApplicationLifetime>(new EmptyApplicationLifetime())
            .AddSingleton<IConfiguration>(config)
            .BuildServiceProvider();
    
        var app = new ApplicationBuilder(serviceProvider);
        app.UseRouting();
        app.UseEndpoints(routes =>
        {
            routes.MapHub<TestHub>("/chat");
        });

        Assert.NotNull(serviceProvider.GetService<HubLifetimeManager<TestHub>>());
    }

    [Fact]
    [Obsolete]
    public void UseAzureSignalRWithAddSignalR()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                {"Azure:SignalR:ConnectionString", DefaultValue}
            })
            .Build();
        var serviceProvider = services.AddLogging()
            .AddSignalR()
            .AddMessagePackProtocol()
            .Services
            .AddSingleton<IHostApplicationLifetime>(new EmptyApplicationLifetime())
            .AddSingleton<IConfiguration>(config)
            .BuildServiceProvider();

        var app = new ApplicationBuilder(serviceProvider);
        Assert.Throws<InvalidOperationException>(() => app.UseAzureSignalR(routes =>
        {
            routes.MapHub<TestHub>("/chat");
        }));
    }

    [Fact]
    [Obsolete]
    public void UseAzureSignalRWithInvalidConnectionString()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                {"Azure:SignalR:ConnectionString", "A=b;c=d"}
            })
            .Build();
        var serviceProvider = services.AddLogging()
            .AddSignalR()
            .AddAzureSignalR()
            .AddMessagePackProtocol()
            .Services
            .AddSingleton<IHostApplicationLifetime>(new EmptyApplicationLifetime())
            .AddSingleton<IConfiguration>(config)
            .BuildServiceProvider();

        var app = new ApplicationBuilder(serviceProvider);
        var exception = Assert.Throws<ArgumentException>(() => app.UseAzureSignalR(routes =>
        {
            routes.MapHub<TestHub>("/chat");
        }));
        Assert.StartsWith("Connection string missing required properties endpoint.", exception.Message);
    }

    [Fact]
    [Obsolete]
    public void UseAzureSignalRWithConnectionStringNotSpecified()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();
        var serviceProvider = services.AddLogging()
            .AddSignalR()
            .AddAzureSignalR()
            .AddMessagePackProtocol()
            .Services
            .AddSingleton<IHostApplicationLifetime>(new EmptyApplicationLifetime())
            .AddSingleton<IConfiguration>(config)
            .BuildServiceProvider();

        var app = new ApplicationBuilder(serviceProvider);
        var exception = Assert.Throws<AzureSignalRConfigurationNoEndpointException>(() => app.UseAzureSignalR(routes =>
        {
            routes.MapHub<TestHub>("/chat");
        }));
        Assert.StartsWith("No connection string was specified.", exception.Message);
    }

    private sealed class SkipIfTargetNetstandard : FactAttribute
    {
        public SkipIfTargetNetstandard()
        {
            if (IsTargetNetStandard())
            {
                Skip = "Not applicable in netstandard 2.0.";
            }
        }

        private static bool IsTargetNetStandard()
        {
            var attribute = typeof(ServiceOptions).Assembly.GetCustomAttributes(typeof(TargetFrameworkAttribute), inherit: true);
            var frameworkAttribute = (TargetFrameworkAttribute)attribute.GetValue(0);
            if (frameworkAttribute.FrameworkName.Contains(".NETStandard", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return false;
        }
    }
}

public class EmptyApplicationLifetime : IHostApplicationLifetime
{
    public CancellationToken ApplicationStarted => CancellationToken.None;

    public CancellationToken ApplicationStopping => CancellationToken.None;

    public CancellationToken ApplicationStopped => CancellationToken.None;

    public void StopApplication()
    {
    }
}
