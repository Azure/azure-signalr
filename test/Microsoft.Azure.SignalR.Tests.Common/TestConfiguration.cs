// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.SignalR.Tests.Common
{
    public class TestConfiguration
    {
        public static readonly TestConfiguration Instance = new TestConfiguration();

        public IConfiguration Configuration { get; }

        public string ConnectionString { get; private set; }

        public string TestEndpoint { get; private set; }

        protected TestConfiguration()
        {
            Configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddUserSecrets<TestConfiguration>(optional: true)
                .AddJsonFile("appsettings.Test.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            Init();
        }

        private void Init()
        {
            ConnectionString = Configuration["Azure:SignalR:ConnectionString"];
            TestEndpoint = Configuration["Azure:SignalR:TestEndpoint"];
        }
    }
}