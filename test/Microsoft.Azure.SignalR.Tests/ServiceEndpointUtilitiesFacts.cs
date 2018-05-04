// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests
{
    public class ServiceEndpointUtilitiesFacts
    {
        [Theory]
        [InlineData("Endpoint=aaa;AccessKey=bbb;")]
        [InlineData("ENDPOINT=aaa/;ACCESSKEY=bbb;")]
        public void ValidConnectionString(string connectionString)
        {
            var endpointUtility = GetServiceEndpointUtility(connectionString);

            Assert.Equal("aaa", endpointUtility.Endpoint);
            Assert.Equal("bbb", endpointUtility.AccessKey);
        }


        [Fact]
        public void EmptyConnectionStrings()
        {
            Exception exception = null;
            try
            {
                GetServiceEndpointUtility("");
            }
            catch (ArgumentException ex)
            {
                exception = ex;
            }

            Assert.NotNull(exception);
            Assert.Contains("No connection string was specified.", exception.Message);
        }

        [Theory]
        [InlineData("Endpoint=xxx")]
        [InlineData("AccessKey=xxx")]
        [InlineData("XXX=yyy")]
        public void InvalidConnectionStrings(string connectionString)
        {
            Exception exception = null;
            try
            {
                GetServiceEndpointUtility(connectionString);
            }
            catch (ArgumentException ex)
            {
                exception = ex;
            }

            Assert.NotNull(exception);
            Assert.Contains("Connection string missing required properties", exception.Message);
        }

        private IServiceEndpointUtility GetServiceEndpointUtility(string connectionString)
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    {"Azure:SignalR:ConnectionString", connectionString}
                })
                .Build();
            var serviceProvider = new ServiceCollection()
                .AddSignalR()
                .AddAzureSignalR()
                .Services
                .AddSingleton<IConfiguration>(config)
                .BuildServiceProvider();
            return serviceProvider.GetRequiredService<IServiceEndpointUtility>();
        }
    }
}
