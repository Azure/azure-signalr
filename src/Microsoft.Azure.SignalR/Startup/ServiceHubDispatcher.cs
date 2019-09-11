// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NETSTANDARD2_0
using System;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceHubDispatcher
    {
        // A late bound version of ServiceHubDispatcher<THub>.
        private readonly Type _serviceDispatcherType = typeof(ServiceHubDispatcher<>);
        private readonly IServiceProvider _serviceProvider;

        public ServiceHubDispatcher(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void Start(Endpoint endpoint, Type hubType, ConnectionDelegate app)
        {
            var type = _serviceDispatcherType.MakeGenericType(hubType);
            var startMethod = type.GetMethod("Start", new Type[] { typeof(ConnectionDelegate), typeof(Action<HttpContext>) });

            Action<HttpContext> configureContext = c => c.Features.Set<IEndpointFeature>(new EndpointFeature
            {
                Endpoint = endpoint
            });

            object dispatcher = _serviceProvider.GetRequiredService(type);

            startMethod.Invoke(dispatcher, new object[] { app, configureContext });
        }

        private class EndpointFeature : IEndpointFeature
        {
            public Endpoint Endpoint { get; set; }
        }
    }
}
#endif