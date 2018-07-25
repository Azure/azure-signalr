// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
using Microsoft.AspNet.SignalR.Infrastructure;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.AspNet.SignalR.Transports;
using Microsoft.Azure.SignalR.AspNet;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Options;

namespace Owin
{
    public static partial class OwinExtensions
    {
        public static IAppBuilder MapAzureSignalR(this IAppBuilder builder)
        {
            return builder.MapAzureSignalR(new HubConfiguration());
        }

        public static IAppBuilder MapAzureSignalR(this IAppBuilder builder, HubConfiguration configuration)
        {
            return builder.MapAzureSignalR("/signalr", configuration);
        }

        public static IAppBuilder MapAzureSignalR(this IAppBuilder builder, string path, HubConfiguration configuration)
        {
            return builder.Map(path, subApp => subApp.RunAzureSignalR(configuration));
        }

        public static void RunAzureSignalR(this IAppBuilder builder)
        {
            builder.RunAzureSignalR(new HubConfiguration());
        }

        public static void RunAzureSignalR(this IAppBuilder builder, HubConfiguration configuration)
        {
            RunAzureSignalR(builder, configuration, ConfigurationManager.ConnectionStrings[ServiceOptions.ConnectionStringDefaultKey]?.ConnectionString);
        }

        public static void RunAzureSignalR(this IAppBuilder builder, HubConfiguration configuration, string connectionString)
        {
            RunAzureSignalR(builder, configuration, s => s.ConnectionString = connectionString);
        }

        public static void RunAzureSignalR(this IAppBuilder builder, HubConfiguration configuration, Action<ServiceOptions> optionsConfigure)
        {
            var serviceOptions = new ServiceOptions();
            optionsConfigure?.Invoke(serviceOptions);
            RunAzureSignalRCore(builder, configuration, serviceOptions);
        }

        private static void RunAzureSignalRCore(IAppBuilder builder, HubConfiguration configuration, ServiceOptions options)
        {
            // Replace default HubDispatcher with a custom one, which has its own negotiation logic
            // https://github.com/SignalR/SignalR/blob/dev/src/Microsoft.AspNet.SignalR.Core/Hosting/PersistentConnectionFactory.cs#L42
            var hubDispatcher = new ServiceHubDispatcher(configuration);
            configuration.Resolver.Register(typeof(PersistentConnection), () => hubDispatcher);
            builder.RunSignalR(typeof(PersistentConnection), configuration);

            RegisterServiceObjects(configuration, options);

            var hubs = GetAvailableHubNames(configuration);
            if (hubs?.Count > 0)
            {
                // Start the server->service connection asynchronously 
                _ = new ConnectionFactory(hubs, configuration).StartAsync();
            }
            else
            {
                // TODO: log something
            }
        }

        private static void RegisterServiceObjects(HubConfiguration configuration, ServiceOptions options)
        {
            // share the same object all through
            var serviceOptions = Options.Create(options);

            var serviceProtocol = new ServiceProtocol();
            var endpoint = new ServiceEndpoint(serviceOptions.Value);
            var provider = new EmptyProtectedData();
            var scm = new ServiceConnectionManager();

            // For safety, ALWAYS register abstract classes or interfaces
            // Some third-party DI frameworks such as Ninject, implicit self-binding concrete types:
            // https://github.com/ninject/ninject/wiki/dependency-injection-with-ninject#skipping-the-type-binding-bit--implicit-self-binding-of-concrete-types
            configuration.Resolver.Register(typeof(IOptions<ServiceOptions>), () => serviceOptions);
            configuration.Resolver.Register(typeof(IServiceEndpoint), () => endpoint);
            configuration.Resolver.Register(typeof(IServiceConnectionManager), () => scm);
            configuration.Resolver.Register(typeof(IProtectedData), () => provider);
            configuration.Resolver.Register(typeof(IMessageBus), () => new ServiceMessageBus(configuration.Resolver));
            configuration.Resolver.Register(typeof(ITransportManager), () => new AzureTransportManager());
            configuration.Resolver.Register(typeof(IServiceProtocol), () => serviceProtocol);
            
            // TODO: Register LoggerFactory
        }

        private static IReadOnlyList<string> GetAvailableHubNames(HubConfiguration configuration)
        {
            var hubManager = configuration.Resolver.Resolve<IHubManager>();
            return hubManager?.GetHubs().Select(s => s.Name).ToList();
        }
    }
}
