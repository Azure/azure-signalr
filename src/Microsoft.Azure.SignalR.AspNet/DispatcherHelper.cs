// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.AspNet.SignalR.Tracing;
using Microsoft.AspNet.SignalR.Transports;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Owin.Infrastructure;
using Owin;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal class DispatcherHelper
    {
        internal static ServiceHubDispatcher PrepareAndGetDispatcher(IAppBuilder builder, HubConfiguration configuration, ServiceOptions options, string applicationName, ILoggerFactory loggerFactory)
        {
            // Ensure we have the conversions for MS.Owin so that
            // the app builder respects the OwinMiddleware base class
            SignatureConversions.AddConversions(builder);

            // ServiceEndpointManager needs the logger
            var hubs = GetAvailableHubNames(configuration);

            var endpoint = new ServiceEndpointManager(options, loggerFactory);
            configuration.Resolver.Register(typeof(IServiceEndpointManager), () => endpoint);

            // Get the one from DI or new a default one
            var router = configuration.Resolver.Resolve<IEndpointRouter>() ?? new DefaultEndpointRouter();

            builder.Use<NegotiateMiddleware>(configuration, applicationName, endpoint, router, options, loggerFactory);

            builder.RunSignalR(configuration);

            // Fetch the trace manager from DI and add logger provider
            var traceManager = configuration.Resolver.Resolve<ITraceManager>();
            if (traceManager != null)
            {
                loggerFactory.AddProvider(new TraceManagerLoggerProvider(traceManager));
            }

            configuration.Resolver.Register(typeof(ILoggerFactory), () => loggerFactory);

            // TODO: Using IOptions looks wierd, thinking of a way removing it
            // share the same object all through
            var serviceOptions = Options.Create(options);

            // For safety, ALWAYS register abstract classes or interfaces
            // Some third-party DI frameworks such as Ninject, implicit self-binding concrete types:
            // https://github.com/ninject/ninject/wiki/dependency-injection-with-ninject#skipping-the-type-binding-bit--implicit-self-binding-of-concrete-types
            configuration.Resolver.Register(typeof(IOptions<ServiceOptions>), () => serviceOptions);

            var serviceProtocol = new ServiceProtocol();
            configuration.Resolver.Register(typeof(IServiceProtocol), () => serviceProtocol);

            // allow override from tests
            var scm = configuration.Resolver.Resolve<IServiceConnectionManager>();
            if (scm == null)
            {
                scm = new ServiceConnectionManager(applicationName, hubs);
                configuration.Resolver.Register(typeof(IServiceConnectionManager), () => scm);
            }

            var ccm = configuration.Resolver.Resolve<IClientConnectionManager>();
            if (ccm == null)
            {
                ccm = new ClientConnectionManager(configuration, loggerFactory);
                configuration.Resolver.Register(typeof(IClientConnectionManager), () => ccm);
            }

            var atm = new AzureTransportManager(configuration.Resolver);
            configuration.Resolver.Register(typeof(ITransportManager), () => atm);

            var parser = new SignalRMessageParser(hubs, configuration.Resolver);
            configuration.Resolver.Register(typeof(IMessageParser), () => parser);

            var smb = new ServiceMessageBus(configuration.Resolver);
            configuration.Resolver.Register(typeof(IMessageBus), () => smb);

            if (hubs?.Count > 0)
            {
                return new ServiceHubDispatcher(hubs, serviceProtocol, scm, ccm, endpoint, router, serviceOptions, loggerFactory);
            }
            else
            {
                loggerFactory.CreateLogger<DispatcherHelper>().Log(LogLevel.Warning, "No hubs found.");
                return null;
            }
        }

        private static IReadOnlyList<string> GetAvailableHubNames(HubConfiguration configuration)
        {
            var hubManager = configuration.Resolver.Resolve<IHubManager>();
            return hubManager?.GetHubs().Select(s => s.Name).ToList();
        }
    }
}
