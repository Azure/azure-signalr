// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    internal class Utility
    {
        public static IServiceManager GenerateServiceManager(string connectionString, ServiceTransportType serviceTransportType = ServiceTransportType.Transient)
        {
            var serviceManagerOptions = new ServiceManagerOptions
            {
                ConnectionString = connectionString,
                ServiceTransportType = serviceTransportType
            };

            return new ServiceManager(serviceManagerOptions);
        }

        public static HubConnection CreateHubConnection(string endpoint, string accessToken) =>
            new HubConnectionBuilder()
                .WithUrl(endpoint, option =>
                {
                    option.AccessTokenProvider = () =>
                    {
                        return Task.FromResult(accessToken);
                    };
                }).Build();
    }
}
