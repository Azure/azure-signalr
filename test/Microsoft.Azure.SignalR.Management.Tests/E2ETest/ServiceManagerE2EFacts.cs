// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Testing.xunit;
using Microsoft.Azure.SignalR.TestsCommon;
using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class ServiceManagerE2EFacts
    {
        private const string HubName = "signalrBench"; 

        [ConditionalFact]
        [SkipIfConnectionStringNotPresent]
        internal async Task ClientConnectionConnectToServiceTest()
        {
            var serviceManagerOptions = new ServiceManagerOptions
            {
                ConnectionString = TestConfiguration.Instance.ConnectionString
            };

            var serviceManager = new ServiceManager(serviceManagerOptions);

            var clientEndpoint = serviceManager.GetClientEndpoint(HubName);

            var connection = new HubConnectionBuilder()
                .WithUrl(clientEndpoint, option =>
                {
                    option.AccessTokenProvider = () =>
                    {
                        var clientAccessToken = serviceManager.GenerateClientAccessToken(HubName);
                        return Task.FromResult(clientAccessToken);
                    };
                }).Build();

            Task task = null;
            try
            {
                await (task = connection.StartAsync());
            }
            finally
            {
                Assert.Null(task.Exception);
            }
        }
    }
}
