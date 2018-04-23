﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.SignalR
{
    public class AzureSignalR
    {
        /// <summary>
        /// Create an instance of ServiceContext.
        /// </summary>
        /// <param name="hubName">Name of the Hub to operate on and it is case insensitive</param>
        /// <returns>An instance of ServiceContext which can be used to send message to the clients who connected on the Hub</returns>
        public static ServiceContext CreateServiceContext(string hubName)
        {
            var connectionString = Environment.GetEnvironmentVariable(ServiceOptions.ConnectionStringDefaultKey);
            return InternalCreateServiceContext(connectionString, hubName);
        }

        /// <summary>
        /// Create an instance of ServiceContext.
        /// </summary>
        /// <param name="connectionString">The string used to connect service.</param>
        /// <param name="hubName">Name of the Hub to operate on and it is case insensitive</param>
        /// <returns>An instance of ServiceContext which can be used to send message to the clients who connected on the Hub</returns>
        public static ServiceContext CreateServiceContext(string connectionString, string hubName)
        {
            return InternalCreateServiceContext(connectionString, hubName);
        }

        private static ServiceContext InternalCreateServiceContext(string connectionString, string hubName)
        {
            if (string.IsNullOrEmpty(hubName))
            {
                throw new ArgumentNullException(nameof(hubName));
            }
            
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging()
                .AddAuthorization()
                // We need to serialize the request with all protocols, that is why we add those protocols
                .AddSignalR()
                .AddJsonProtocol()
                .AddMessagePackProtocol()
                .AddAzureSignalR(connectionString);
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var serviceEndpointUtility = serviceProvider.GetRequiredService<IServiceEndpointUtility>();
            var hubMessageSender = serviceProvider.GetRequiredService<IHubMessageSender>();
            var serviceHubContext = new ServiceHubContext(hubName, serviceEndpointUtility, hubMessageSender);
            return new ServiceContext(hubName, serviceEndpointUtility, serviceHubContext);
        }
    }
}
