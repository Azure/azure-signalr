// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.SignalR.Startup
{
    internal class ServiceHubDispatcher
    {
        // A late bound version of ServiceHubDispatcher<THub>.
        private readonly Type _serviceDispatcherType = typeof(ServiceOptions).Assembly.GetType("Microsoft.Azure.SignalR.ServiceHubDispatcher`1");
        private readonly IServiceProvider _serviceProvider;

        public ServiceHubDispatcher(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void Start(Type hubType, ConnectionDelegate app)
        {
            var type = _serviceDispatcherType.MakeGenericType(hubType);
            var startMethod = type.GetMethod("Start");

            object dispatcher = _serviceProvider.GetRequiredService(type);

            startMethod.Invoke(dispatcher, new object[] { app });
        }
    }
}
