using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Azure.SignalR.ServerlessAgent.Tests
{
    public class ServerlessAgentTest
    {
        [Fact]
        public async Task BuildServerlessAgentWithToken()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddUserSecrets<ServerlessAgentTest>()
                .Build();

            var connectionString = configuration["Azure:SignalR:ConnectionString"];
            var endpoint = connectionString.Split(";")[0].Split("=")[1];
            var hubName = "signalrbench";

            var accessToken = AccessTokenGenerator.CenerateAccessTokenForBroadcast(connectionString, hubName);
            
            var builder = new ServerlessAgentBuilder().WithEndpoint(endpoint).WithAccessToken(accessToken).UseRestV1();
            var agent = builder.BuildAsync(hubName);
            await agent.Clients.All.SendAsync("SendMessage", "zzz", "xxx");            
        }

        [Fact]
        public async Task BuildServerlessAgentWithConnectionString()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddUserSecrets<ServerlessAgentTest>()
                .Build();

            var connectionString = configuration["Azure:SignalR:ConnectionString"];
            var hubName = "signalrbench";

            var builder = new ServerlessAgentBuilder().WithConnectionString(connectionString).UseRestV1();
            var agent = builder.BuildAsync(hubName);
            await agent.Clients.All.SendAsync("SendMessage", "zzz", "xxx");
        }
    }
}
