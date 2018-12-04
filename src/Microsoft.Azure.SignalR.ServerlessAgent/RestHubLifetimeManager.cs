using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.ServerlessAgent
{
    public class RestHubLifetimeManager<THub> : HubLifetimeManager<THub> where THub : Hub
    {
        private AgentContext _context;

        public RestHubLifetimeManager(AgentContext agentContext)
        {
            _context = agentContext;
        }

        public override Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public override Task OnConnectedAsync(HubConnectionContext connection)
        {
            throw new NotImplementedException();
        }

        public override Task OnDisconnectedAsync(HubConnectionContext connection)
        {
            throw new NotImplementedException();
        }

        public override Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public override async Task SendAllAsync(string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            var apis = new RestApis(_context.GetEndpoint(), _context.HubName, _context.RestApiVersion);
            var api = apis.Broadcast();
            var success = _context.Credentail.TryGetFirstTokenForAudience(api, out var accessToken);
            if (!success)
            {
                throw new Exception("Cannot find valid access token");
            }

            var payload = new PayloadMessage { Target = methodName, Arguments = args };
            var request = new HttpRequest(api, payload, accessToken);
            var response = await request.SendAsync();
            if (response.StatusCode != HttpStatusCode.Accepted)
            {
                throw new Exception($"Response failed. Stauts code: {response.StatusCode}. Reason: {response.ReasonPhrase}");
            }
        }

        public override Task SendAllExceptAsync(string methodName, object[] args, IReadOnlyList<string> excludedConnectionIds, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public override Task SendConnectionAsync(string connectionId, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public override Task SendConnectionsAsync(IReadOnlyList<string> connectionIds, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public override Task SendGroupAsync(string groupName, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public override Task SendGroupExceptAsync(string groupName, string methodName, object[] args, IReadOnlyList<string> excludedConnectionIds, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public override Task SendGroupsAsync(IReadOnlyList<string> groupNames, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public override Task SendUserAsync(string userId, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public override Task SendUsersAsync(IReadOnlyList<string> userIds, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
