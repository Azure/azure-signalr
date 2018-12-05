using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Microsoft.AspNetCore.Testing.xunit;

namespace Microsoft.Azure.SignalR.ServerlessAgent.Tests
{
    public class ServerlessAgentTest
    {
        private string _hubName = "signalrbench";
        private string _methodName = "SendMessage";
        private string _groupName = "groupName";
        private string _userId = "wanl1";

        public ServerlessAgentTest()
        {
            var configuration = TestConfiguration.Instance;
        }

        [ConditionalFact]
        [SkipIfConnectionStringNotPresent]
        public async Task BuildServerlessAgentWithToken()
        {
            var connectionString = TestConfiguration.Instance.ConnectionString;
            var endpoint = ParseEndpoint(connectionString);
            var accessToken = AccessTokenGenerator.CenerateAccessTokenForBroadcast(connectionString, _hubName);
            
            var builder = new ServerlessAgentBuilder().WithEndpoint(endpoint).WithAccessToken(accessToken).UseRestV1();
            var agent = builder.BuildAsync(_hubName);
            await agent.Clients.All.SendAsync(_methodName, "server", "message");            
        }

        [Fact]
        public async Task BuildServerlessAgentWithConnectionString()
        {
            var connectionString = TestConfiguration.Instance.ConnectionString;
            var builder = new ServerlessAgentBuilder().WithConnectionString(connectionString).UseRestV1();
            var agent = builder.BuildAsync(_hubName);
            await agent.Clients.All.SendAsync(_methodName, "server", "message");
        }

        [Fact]
        public async Task SendToUserScenario()
        {
            var connectionString = TestConfiguration.Instance.ConnectionString;
            var builder = new ServerlessAgentBuilder().WithConnectionString(connectionString).UseRestV1();
            var agent = builder.BuildAsync(_hubName);
            await agent.Clients.User(_userId).SendAsync(_methodName, "server", "message sent from serverless agent");
        }

        [Fact]
        public async Task GroupScenario()
        {
            var connectionString = TestConfiguration.Instance.ConnectionString;
            var builder = new ServerlessAgentBuilder().WithConnectionString(connectionString).UseRestV1();
            var agent = builder.BuildAsync(_hubName);

            await agent.UserGroups.AddToGroupAsync(_userId, _groupName);
            await agent.Clients.Group(_groupName).SendAsync(_methodName, "server", "message sent from serverless agent");
            await agent.UserGroups.RemoveFromGroupAsync(_userId, _groupName);
            await agent.Clients.Group(_groupName).SendAsync(_methodName, "server", "message sent from serverless agent");
        }

        private string ParseEndpoint(string connectionString)
        {
            var endpoint = connectionString.Split(";")[0].Split("=")[1];
            return endpoint;
        }
    }
}
