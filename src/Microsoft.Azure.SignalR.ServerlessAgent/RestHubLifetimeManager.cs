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
    public class RestHubLifetimeManager : HubLifetimeManagerBase<Hub>, IHubLifetimeManagerExtension
    {
        public RestHubLifetimeManager(AgentContext agentContext)
        {
            _context = agentContext;
            _apis = new RestApis(_context.GetEndpoint(), _context.HubName, _context.RestApiVersion);
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
            var api = _apis.Broadcast();
            await SendAsyncInternal(api, methodName, args, cancellationToken);
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

        public override async Task SendGroupAsync(string groupName, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            var api = _apis.SendToGroup(groupName);
            await SendAsyncInternal(api, methodName, args, cancellationToken);
        }

        public override Task SendGroupExceptAsync(string groupName, string methodName, object[] args, IReadOnlyList<string> excludedConnectionIds, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public override Task SendGroupsAsync(IReadOnlyList<string> groupNames, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public override async Task SendUserAsync(string userId, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            var api = _apis.SendToUser(userId);
            await SendAsyncInternal(api, methodName, args, cancellationToken);
        }

        public override Task SendUsersAsync(IReadOnlyList<string> userIds, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public async Task UserAddToGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default)
        {
            var api = _apis.UserGroupManagement(userId, groupName);
            await ManageAsyncInternal(api, HttpMethod.Put);
        }

        public async Task UserRemoveFromGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default)
        {
            var api = _apis.UserGroupManagement(userId, groupName);
            await ManageAsyncInternal(api, HttpMethod.Delete);
        }

        private async Task ManageAsyncInternal(string api, HttpMethod httpMethod)
        {
            var success = _context.Credentail.TryGetFirstTokenForAudience(api, out var accessToken);
            if (!success)
            {
                throw new Exception("Cannot find valid access token");
            }

            var request = new HttpRequest(api, null, accessToken, httpMethod);
            var response = await request.SendAsync();
            if (response.StatusCode != HttpStatusCode.Accepted)
            {
                throw new Exception($"Response failed. Stauts code: {response.StatusCode}. Reason: {response.ReasonPhrase}");
            }
        }

        private async Task SendAsyncInternal(string api, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            var success = _context.Credentail.TryGetFirstTokenForAudience(api, out var accessToken);
            if (!success)
            {
                throw new Exception("Cannot find valid access token");
            }

            var payload = new PayloadMessage { Target = methodName, Arguments = args };
            var request = new HttpRequest(api, payload, accessToken, HttpMethod.Post);
            var response = await request.SendAsync();
            if (response.StatusCode != HttpStatusCode.Accepted)
            {
                throw new Exception($"Response failed. Stauts code: {response.StatusCode}. Reason: {response.ReasonPhrase}");
            }
        }
    }
}
