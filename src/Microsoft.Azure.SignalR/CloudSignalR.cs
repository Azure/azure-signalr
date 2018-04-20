﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.SignalR
{
    public class CloudSignalR
    {
        public static SignalRServiceContext CreateServiceContext(string connectionString, string hubName)
        {
            return CreateServiceContextInternal(connectionString, hubName);
        }

        private static SignalRServiceContext CreateServiceContextInternal(string connectionString, string hubName)
        {
            var serviceCollection = new ServiceCollection();

            serviceCollection.AddLogging();
            serviceCollection.AddAuthorization();
            serviceCollection.AddSignalR().AddAzureSignalR().AddJsonProtocol().AddMessagePackProtocol();
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var connectionServiceProvider = serviceProvider.GetService<IConnectionServiceProvider>();
            var hubMessageSender = serviceProvider.GetService<IHubMessageSender>();
            var signalrServiceHubContext = new SignalRServiceHubContext(connectionServiceProvider, hubMessageSender, hubName);
            return new SignalRServiceContext(connectionServiceProvider, signalrServiceHubContext);
        }
    }
}
