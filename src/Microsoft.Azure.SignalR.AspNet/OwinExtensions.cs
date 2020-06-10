// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.AspNet.SignalR;
using Microsoft.Azure.SignalR.AspNet;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Extensions.Logging.Abstractions;

namespace Owin
{
    public static partial class OwinExtensions
    {
        /// <summary>
        /// Maps Azure SignalR hubs to the app builder pipeline at "/signalr".
        /// </summary>
        /// <param name="builder">The app builder <see cref="IAppBuilder"/>.</param>
        /// <param name="applicationName">The name of your app, it is case-insensitive.</param>
        /// <returns>The app builder</returns>
        /// <remarks>
        /// The connection string is read from the ConnectionString section of application config (web.config or app.config), with name "Azure:SignalR:ConnectionString"
        /// </remarks>
        public static IAppBuilder MapAzureSignalR(this IAppBuilder builder, string applicationName)
        {
            return builder.MapAzureSignalR(applicationName, new HubConfiguration());
        }

        /// <summary>
        /// Maps Azure SignalR hubs to the app builder pipeline at "/signalr".
        /// </summary>
        /// <param name="builder">The app builder <see cref="IAppBuilder"/>.</param>
        /// <param name="applicationName">The name of your app, it is case-insensitive.</param>
        /// <param name="connectionString">The connection string of an Azure SignalR Service instance.</param>
        /// <returns>The app builder</returns>
        public static IAppBuilder MapAzureSignalR(this IAppBuilder builder, string applicationName, string connectionString)
        {
            return builder.MapAzureSignalR(applicationName, options => options.ConnectionString = connectionString);
        }

        /// <summary>
        /// Maps Azure SignalR hubs to the app builder pipeline at "/signalr".
        /// </summary>
        /// <param name="builder">The app builder <see cref="IAppBuilder"/>.</param>
        /// <param name="applicationName">The name of your app, it is case-insensitive.</param>
        /// <param name="optionsConfigure">A callback to configure the <see cref="ServiceOptions"/>.</param>
        /// <returns>The app builder</returns>
        public static IAppBuilder MapAzureSignalR(this IAppBuilder builder, string applicationName, Action<ServiceOptions> optionsConfigure)
        {
            return builder.MapAzureSignalR(applicationName, new HubConfiguration(), optionsConfigure);
        }

        /// <summary>
        /// Maps Azure SignalR hubs to the app builder pipeline at "/signalr".
        /// </summary>
        /// <param name="builder">The app builder <see cref="IAppBuilder"/>.</param>
        /// <param name="applicationName">The name of your app, it is case-insensitive.</param>
        /// <param name="configuration">The hub configuration <see cref="HubConfiguration"/>.</param>
        /// <returns>The app builder</returns>
        public static IAppBuilder MapAzureSignalR(this IAppBuilder builder, string applicationName, HubConfiguration configuration)
        {
            return builder.MapAzureSignalR("/signalr", applicationName, configuration);
        }

        /// <summary>
        /// Maps Azure SignalR hubs to the app builder pipeline at "/signalr".
        /// </summary>
        /// <param name="builder">The app builder <see cref="IAppBuilder"/>.</param>
        /// <param name="applicationName">The name of your app, it is case-insensitive.</param>
        /// <param name="configuration">The hub configuration <see cref="HubConfiguration"/>.</param>
        /// <param name="optionsConfigure">A callback to configure the <see cref="ServiceOptions"/>.</param>
        /// <returns>The app builder</returns>
        public static IAppBuilder MapAzureSignalR(this IAppBuilder builder, string applicationName, HubConfiguration configuration, Action<ServiceOptions> optionsConfigure)
        {
            return builder.MapAzureSignalR("/signalr", applicationName, configuration, optionsConfigure);
        }

        /// <summary>
        /// Maps Azure SignalR hubs to the app builder pipeline at the specified path.
        /// </summary>
        /// <param name="builder">The app builder <see cref="IAppBuilder"/>.</param>
        /// <param name="path">The path to map signalr hubs.</param>
        /// <param name="applicationName">The name of your app, it is case-insensitive.</param>
        /// <param name="configuration">The hub configuration <see cref="HubConfiguration"/>.</param>
        /// <returns>The app builder</returns>
        public static IAppBuilder MapAzureSignalR(this IAppBuilder builder, string path, string applicationName, HubConfiguration configuration)
        {
            return builder.Map(path, subApp => subApp.RunAzureSignalR(applicationName, configuration));
        }

        /// <summary>
        /// Maps Azure SignalR hubs to the app builder pipeline at the specified path.
        /// </summary>
        /// <param name="builder">The app builder <see cref="IAppBuilder"/>.</param>
        /// <param name="path">The path to map signalr hubs.</param>
        /// <param name="applicationName">The name of your app, it is case-insensitive.</param>
        /// <param name="configuration">The hub configuration <see cref="HubConfiguration"/>.</param>
        /// <param name="optionsConfigure">A callback to configure the <see cref="ServiceOptions"/>.</param>
        /// <returns>The app builder</returns>
        public static IAppBuilder MapAzureSignalR(this IAppBuilder builder, string path, string applicationName, HubConfiguration configuration, Action<ServiceOptions> optionsConfigure)
        {
            return builder.Map(path, subApp => subApp.RunAzureSignalR(applicationName, configuration, optionsConfigure));
        }

        /// <summary>
        /// Adds Azure SignalR hubs to the app builder pipeline at "/signalr".
        /// </summary>
        /// <param name="builder">The app builder <see cref="IAppBuilder"/>.</param>
        /// <param name="applicationName">The name of your app, it is case-insensitive.</param>
        /// <remarks>
        /// The connection string is read from the ConnectionString section of application config (web.config or app.config), with name "Azure:SignalR:ConnectionString"
        /// </remarks>
        public static void RunAzureSignalR(this IAppBuilder builder, string applicationName)
        {
            builder.RunAzureSignalR(applicationName, new HubConfiguration());
        }

        /// <summary>
        /// Adds Azure SignalR hubs to the app builder pipeline at "/signalr".
        /// </summary>
        /// <param name="builder">The app builder <see cref="IAppBuilder"/>.</param>
        /// <param name="applicationName">The name of your app, it is case-insensitive.</param>
        /// <param name="connectionString">The connection string of an Azure SignalR Service instance.</param>
        public static void RunAzureSignalR(this IAppBuilder builder, string applicationName, string connectionString)
        {
            RunAzureSignalR(builder, applicationName, connectionString, new HubConfiguration());
        }

        /// <summary>
        /// Adds Azure SignalR hubs to the app builder pipeline at "/signalr" using the connection string specified in web.config 
        /// </summary>
        /// <param name="builder">The app builder <see cref="IAppBuilder"/>.</param>
        /// <param name="applicationName">The name of your app, it is case-insensitive.</param>
        /// <param name="configuration">The hub configuration <see cref="HubConfiguration"/>.</param>
        public static void RunAzureSignalR(this IAppBuilder builder, string applicationName, HubConfiguration configuration)
        {
            RunAzureSignalR(builder, applicationName, configuration, null);
        }

        /// <summary>
        /// Adds Azure SignalR hubs to the app builder pipeline at "/signalr".
        /// </summary>
        /// <param name="builder">The app builder <see cref="IAppBuilder"/>.</param>
        /// <param name="applicationName">The name of your app, it is case-insensitive.</param>
        /// <param name="connectionString">The connection string of an Azure SignalR Service instance.</param>
        /// <param name="configuration">The hub configuration <see cref="HubConfiguration"/>.</param>
        public static void RunAzureSignalR(this IAppBuilder builder, string applicationName, string connectionString, HubConfiguration configuration)
        {
            RunAzureSignalR(builder, applicationName, configuration, s => s.ConnectionString = connectionString);
        }


        /// <summary>
        /// Adds Azure SignalR hubs to the app builder pipeline at "/signalr".
        /// </summary>
        /// <param name="builder">The app builder</param>
        /// <param name="applicationName">The name of your app, it is case-insensitive</param>
        /// <param name="optionsConfigure">A callback to configure the <see cref="ServiceOptions"/>.</param>
        public static void RunAzureSignalR(this IAppBuilder builder, string applicationName, Action<ServiceOptions> optionsConfigure)
        {
            RunAzureSignalR(builder, applicationName, new HubConfiguration(), optionsConfigure);
        }

        /// <summary>
        /// Adds Azure SignalR hubs to the app builder pipeline at "/signalr".
        /// </summary>
        /// <param name="builder">The app builder</param>
        /// <param name="applicationName">The name of your app, it is case-insensitive</param>
        /// <param name="configuration">The hub configuration</param>
        /// <param name="optionsConfigure">A callback to configure the <see cref="ServiceOptions"/>.</param>
        public static void RunAzureSignalR(this IAppBuilder builder, string applicationName, HubConfiguration configuration, Action<ServiceOptions> optionsConfigure)
        {
            var serviceOptions = new ServiceOptions();
            optionsConfigure?.Invoke(serviceOptions);
            RunAzureSignalRCore(builder, applicationName, configuration, serviceOptions);
        }

        private static void RunAzureSignalRCore(IAppBuilder builder, string applicationName, HubConfiguration configuration, ServiceOptions options)
        {
            // applicationName is case insensitive, it will be lower cased in the service side
            if (string.IsNullOrEmpty(applicationName))
            {
                throw new ArgumentException("Empty application name is not allowed.", nameof(applicationName));
            }

            options.ApplicationName = applicationName;

            if (configuration == null)
            {
                // Keep the same as SignalR's exception
                throw new ArgumentException("A configuration object must be specified.");
            }

            // MaxPollInterval should be [1,300] seconds
            if (options.MaxPollIntervalInSeconds.HasValue
                && (options.MaxPollIntervalInSeconds < 1 || options.MaxPollIntervalInSeconds > 300))
            {
                throw new AzureSignalRInvalidServiceOptionsException("MaxPollIntervalInSeconds", "[1,300]");
            }

            var loggerFactory = DispatcherHelper.GetLoggerFactory(configuration) ?? NullLoggerFactory.Instance;

            var dispatcher = DispatcherHelper.PrepareAndGetDispatcher(builder, configuration, options, applicationName, loggerFactory);
            if (dispatcher != null)
            {
                // Start the server->service connection asynchronously 
                _ = dispatcher.StartAsync();
            }
        }
    }
}
