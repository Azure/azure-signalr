// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Tests
{
    internal class TestOptionsMonitor : IOptionsMonitor<ServiceOptions>
    {
        private readonly IOptionsMonitor<ServiceOptions> _monitor;

        public TestOptionsMonitor()
        {
            var config = new ConfigurationBuilder().Build();

            var services = new ServiceCollection();
            var endpoints = new List<ServiceEndpoint>() { new ServiceEndpoint($"Endpoint=https://testconnectionstring;AccessKey=1") };
            var serviceProvider = services.AddLogging()
                .AddSignalR().AddAzureSignalR(o => o.Endpoints = endpoints.ToArray())
                .Services
                .AddSingleton<IConfiguration>(config)
                .BuildServiceProvider();
            _monitor = serviceProvider.GetRequiredService<IOptionsMonitor<ServiceOptions>>();
        }

        public ServiceOptions CurrentValue => _monitor.CurrentValue;

        public ServiceOptions Get(string name) => _monitor.Get(name);

        public IDisposable OnChange(Action<ServiceOptions, string> listener) => _monitor.OnChange(listener);
    }
}
