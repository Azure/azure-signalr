﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Builder.Internal;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests
{
    public class AzureSignalRMarkerServiceFact
    {
        private const string DefaultValue = "Endpoint=https://abc;AccessKey=abc123;";

        [Fact]
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

        [Fact]
        public void UseSignalRWithAddAzureSignalR()
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
                .AddSingleton<IApplicationLifetime>(new EmptyApplicationLifetime())
                .AddSingleton<IConfiguration>(config)
                .BuildServiceProvider();

            var app = new ApplicationBuilder(serviceProvider);
            app.UseSignalR(routes =>
            {
                Assert.Throws<InvalidOperationException>(() => routes.MapHub<TestHub>("/chat"));
            });
        }

        [Fact]
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
                .AddSingleton<IApplicationLifetime>(new EmptyApplicationLifetime())
                .AddSingleton<IConfiguration>(config)
                .BuildServiceProvider();

            var app = new ApplicationBuilder(serviceProvider);
            Assert.Throws<InvalidOperationException>(() => app.UseAzureSignalR(routes =>
            {
                routes.MapHub<TestHub>("/chat");
            }));
        }

        [Fact]
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
                .AddSingleton<IApplicationLifetime>(new EmptyApplicationLifetime())
                .AddSingleton<IConfiguration>(config)
                .BuildServiceProvider();

            var app = new ApplicationBuilder(serviceProvider);
            var exception = Assert.Throws<ArgumentException>(() => app.UseAzureSignalR(routes =>
            {
                routes.MapHub<TestHub>("/chat");
            }));
            Assert.StartsWith("Connection string missing required properties endpoint and accesskey.", exception.Message);
        }

        [Fact]
        public void UseAzureSignalRWithConnectionStringNotSpecified()
        {
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder().Build();
            var serviceProvider = services.AddLogging()
                .AddSignalR()
                .AddAzureSignalR()
                .AddMessagePackProtocol()
                .Services
                .AddSingleton<IApplicationLifetime>(new EmptyApplicationLifetime())
                .AddSingleton<IConfiguration>(config)
                .BuildServiceProvider();

            var app = new ApplicationBuilder(serviceProvider);
            var exception = Assert.Throws<ArgumentException>(() => app.UseAzureSignalR(routes =>
            {
                routes.MapHub<TestHub>("/chat");
            }));
            Assert.StartsWith("No connection string was specified.", exception.Message);
        }
    }

    public class EmptyApplicationLifetime : IApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => CancellationToken.None;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication()
        {
        }
    }
}
