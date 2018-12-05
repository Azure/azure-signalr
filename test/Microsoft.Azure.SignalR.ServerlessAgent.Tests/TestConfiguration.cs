using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Microsoft.Azure.SignalR.ServerlessAgent.Tests
{
    public class TestConfiguration
    {
        public static readonly TestConfiguration Instance = new TestConfiguration();
        private readonly IConfiguration _configuration;
        public string ConnectionString { get; private set; }

        protected TestConfiguration()
        {
            _configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddUserSecrets<ServerlessAgentTest>(optional: true)
                .Build();

            Init();
        }


        private void Init()
        {
            ConnectionString = _configuration["Azure:SignalR:ConnectionString"];

        }
    }
}
