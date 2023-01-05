// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.Management
{
    /// <summary>
    /// A builder for configuring <see cref="ServiceManager"/> instances.
    /// </summary>
#pragma warning disable CS0618 // Type or member is obsolete
    public class ServiceManagerBuilder : IServiceManagerBuilder
#pragma warning restore CS0618 // Type or member is obsolete
    {
        private readonly IServiceCollection _services;
        private Action<IServiceCollection> _configureAction;

        internal ServiceManagerBuilder(IServiceCollection services)
        {
            _services = services;
        }

        public ServiceManagerBuilder() : this(new ServiceCollection())
        {
            _services.AddSignalRServiceManager();
        }

        /// <summary>
        /// Registers an action used to configure <see cref="ServiceManager"/>.
        /// </summary>
        /// <param name="configure">A callback to configure the <see cref="ServiceManager"/>.</param>
        /// <returns>The same instance of the <see cref="ServiceManagerBuilder"/> for chaining.</returns>
        public ServiceManagerBuilder WithOptions(Action<ServiceManagerOptions> configure)
        {
            _services.Configure(configure);
            return this;
        }

        public ServiceManagerBuilder WithLoggerFactory(ILoggerFactory loggerFactory)
        {
            _services.AddSingleton(loggerFactory);
            return this;
        }

        public ServiceManagerBuilder WithConfiguration(IConfiguration configuration)
        {
            _services.AddSingleton(configuration);
            return this;
        }

        public ServiceManagerBuilder WithRouter(IEndpointRouter router)
        {
            _services.AddSingleton(router);
            return this;
        }

        /// <summary>
        /// Uses Newtonsoft.Json library to serialize messages sent to SignalR.
        /// </summary>
        /// <param name="configure">A delegate that can be used to configure the <see cref="NewtonsoftServiceHubProtocolOptions"/>.</param>
        /// <returns>The <see cref="ServiceManagerBuilder"/> instance itself.</returns>
        public ServiceManagerBuilder WithNewtonsoftJson(Action<NewtonsoftServiceHubProtocolOptions> configure)
        {
            if (configure is null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            _services.AddNewtonsoftHubProtocol(configure);
            return this;
        }

        /// <summary>
        /// Uses Newtonsoft.Json library to serialize messages sent to SignalR clients.
        /// </summary>
        /// <returns>The <see cref="ServiceHubContextBuilder"/> instance itself.</returns>
        public ServiceManagerBuilder WithNewtonsoftJson()
        {
            return WithNewtonsoftJson(o => { });
        }

        /// <summary>
        /// Sets the SignalR hub protocols to serialize messages sent to SignalR clients.
        /// </summary>
        /// <remarks><para>Currently it only works for <b>persistent</b> mode. The support for <b>transiet(default)</b> mode is to be done. </para> 
        /// <para>Calling this method first clears the existing hub protocols, then adds the new protocols.</para></remarks>
        /// <param name="hubProtocols">Only the protocols named "json" or "messagepack" are allowed.</param>
        /// <returns>The <see cref="ServiceHubContextBuilder"/> instance itself.</returns>
        public ServiceManagerBuilder WithHubProtocols(params IHubProtocol[] hubProtocols)
        {
            if (hubProtocols == null)
            {
                throw new ArgumentNullException(nameof(hubProtocols));
            }
            // Allows the user to use MessagePack only.
            _services.RemoveAll<IHubProtocol>();
            foreach (var hubProtocol in hubProtocols)
            {
                if (hubProtocol == null)
                {
                    throw new ArgumentNullException(nameof(hubProtocols), $"Null hub protocol is not allowed.");
                }
                if (!hubProtocol.Name.Equals(Constants.Protocol.Json, StringComparison.OrdinalIgnoreCase) && !hubProtocol.Name.Equals(Constants.Protocol.MessagePack, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException($"The name '{hubProtocol.Name}' of the hub protocol is not supported. Only '{Constants.Protocol.Json}' or '{Constants.Protocol.MessagePack}' is allowed.");
                }
                _services.TryAddEnumerable(ServiceDescriptor.Singleton(hubProtocol));
            }
            return this;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ServiceManagerBuilder WithCallingAssembly()
        {
            var assembly = Assembly.GetCallingAssembly();
            _services.WithAssembly(assembly);
            return this;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ServiceManagerBuilder AddUserAgent(string userAgent)
        {
            _services.AddUserAgent(userAgent);
            return this;
        }

        internal ServiceManagerBuilder ConfigureServices(Action<IServiceCollection> configureAction)
        {
            _configureAction = configureAction;
            return this;
        }

        /// <summary>
        /// Builds <see cref="IServiceManager"/> instances.
        /// </summary>
        /// <returns>The instance of the <see cref="IServiceManager"/>.</returns>
        [Obsolete("Use BuildServiceManager() instead. See https://github.com/Azure/azure-signalr/blob/dev/docs/management-sdk-migration.md for migration guide.")]
        public IServiceManager Build()
        {
            return (IServiceManager)BuildServiceManager();
        }

        /// <summary>
        /// Builds <see cref="ServiceManager"/> instances.
        /// </summary>
        /// <returns>The instance of the <see cref="ServiceManager"/>.</returns>
        public ServiceManager BuildServiceManager()
        {
            var serviceCollection = new ServiceCollection().Add(_services);
            _configureAction?.Invoke(serviceCollection);
            serviceCollection.AddSingleton(serviceCollection.ToList() as IReadOnlyCollection<ServiceDescriptor>);
            return serviceCollection.BuildServiceProvider()
                .GetRequiredService<IServiceManager>() as ServiceManager;
        }
    }
}