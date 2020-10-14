// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Management.MultiEndpoints;
using Microsoft.Azure.SignalR.Tests;
using Microsoft.Azure.SignalR.Tests.Common;
using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class MultiServiceManagerTests
    {
        private const string ProductInfo = "productInfo";
        private const string HubName = "hub-1";
        private static readonly ServiceManagerOptions ServiceManagerOptions = new ServiceManagerOptions
        {
            ServiceEndpoints = FakeEndpointConstant.FakeServiceEndpoints,
            ServiceTransportType = ServiceTransportType.Persistent
        };
        private static readonly ServiceOptions ServiceOptions = new ServiceOptions
        {
            Endpoints = ServiceManagerOptions.ServiceEndpoints
        };

        [Fact]
        public async Task CreateHubContextFact()
        {
            var multiServiceManager = new MultiServiceManager(ServiceManagerOptions, ProductInfo, null);
            var serviceHubContext = await multiServiceManager.CreateHubContextAsync(HubName);
            await serviceHubContext.DisposeAsync();
        }

        [Fact]
        public void GetClientEndpointURLFact()
        {
            var endpoint = ServiceManagerOptions.ServiceEndpoints[0];
            var multiServiceManager = new MultiServiceManager(ServiceManagerOptions, ProductInfo, null);
            var endpointUrl = multiServiceManager.GetClientEndpoint(HubName, endpoint);

            var endpointProvider = new ServiceEndpointProvider(new DefaultServerNameProvider(), endpoint, ServiceOptions);

            Assert.Equal(endpointProvider.GetClientEndpoint(HubName, null, null), endpointUrl);
        }

        [Fact]
        public void GetClientAccessTokenFact()
        {
            var endpoint = ServiceManagerOptions.ServiceEndpoints[0];
            var multiServiceManager = new MultiServiceManager(ServiceManagerOptions, ProductInfo, null);
            var tokenString = multiServiceManager.GenerateClientAccessToken(HubName, endpoint);

            var endpointProvider = new ServiceEndpointProvider(new DefaultServerNameProvider(), endpoint, ServiceOptions);
            var token = JwtTokenHelper.JwtHandler.ReadJwtToken(tokenString);
            var expectedToken = JwtTokenHelper.GenerateExpectedAccessToken(token, endpointProvider.GetClientEndpoint(HubName, null, null), endpoint.AccessKey, null);

            Assert.Equal(expectedToken, tokenString);
        }
    }
}