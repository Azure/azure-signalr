// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Common;
using Newtonsoft.Json;

namespace Microsoft.Azure.SignalR.Management
{
    internal class RestHubLifetimeManager : HubLifetimeManager<Hub>, IHubLifetimeManagerForUserGroup
    {
        private readonly RestApiProvider _restApiProvider;

        public RestHubLifetimeManager(ServiceManagerOptions serviceManagerOptions, string hubName)
        {
            _restApiProvider = new RestApiProvider(serviceManagerOptions.ConnectionString, hubName);
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
            var api = _restApiProvider.GetBroadcastEndpoint();
            var request = BuildRequest(api, HttpMethod.Post, methodName, args, cancellationToken);
            await CallRestApiAsync(request, api?.Audience);
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
            var api = _restApiProvider.GetSendToGroupEndpoint(groupName);
            var request = BuildRequest(api, HttpMethod.Post, methodName, args, cancellationToken);
            await CallRestApiAsync(request, api?.Audience);
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
            var api = _restApiProvider.GetSendToUserEndpoint(userId);
            var request = BuildRequest(api, HttpMethod.Post, methodName, args, cancellationToken);
            await CallRestApiAsync(request, api?.Audience);
        }

        public override Task SendUsersAsync(IReadOnlyList<string> userIds, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public async Task UserAddToGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default)
        {
            var api = _restApiProvider.GetUserGroupManagementEndpoint(userId, groupName);
            var request = BuildRequest(api, HttpMethod.Put, null, null, cancellationToken);
            await CallRestApiAsync(request, api?.Audience);
        }

        public async Task UserRemoveFromGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default)
        {
            var api = _restApiProvider.GetUserGroupManagementEndpoint(userId, groupName);
            var request = BuildRequest(api, HttpMethod.Delete, null, null, cancellationToken);
            await CallRestApiAsync(request, api?.Audience);
        }

        private static HttpRequestMessage GenerateHttpRequest(string url, PayloadMessage payload, string tokenString, HttpMethod httpMethod)
        {
            var request = new HttpRequestMessage(httpMethod, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenString);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            return request;
        }

        private static void ThrowException(Exception innerException, HttpStatusCode? statusCode, string requestUri, string detail = null)
        {
            switch(statusCode)
            {
                case HttpStatusCode.BadRequest:
                    {
                        throw new AzureSignalRBadRequestException(requestUri, innerException, detail);
                    }
                case HttpStatusCode.Unauthorized:
                    {
                        throw new AzureSignalRUnauthorizationException(requestUri, innerException);
                    }
                case HttpStatusCode.NotFound:
                    {
                        throw new AzureSignalRUnableToAccessException(requestUri, innerException);
                    }
                default:
                    {
                        throw new AzureSignalRRuntimeException(requestUri, innerException);
                    }
            }
        }

        private static HttpRequestMessage BuildRequest(RestApiEndpoint endpoint, HttpMethod httpMethod, string methodName = null, object[] args = null, CancellationToken cancellationToken = default)
        {
            var payload = httpMethod == HttpMethod.Post ? new PayloadMessage { Target = methodName, Arguments = args } : null;
            var request = GenerateHttpRequest(endpoint.Audience, payload, endpoint.Token, HttpMethod.Post);
            return request;
        }

        private static async Task CallRestApiAsync(HttpRequestMessage request, string audience)
        {
            var httpClient = HttpClientFactory.CreateClient();
            HttpResponseMessage response = null;

            try
            {
                response = await httpClient.SendAsync(request);
            }
            catch (HttpRequestException ex)
            {
                ThrowException(ex, HttpStatusCode.NotFound, audience);
            }

            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                var detail = await response.Content.ReadAsStringAsync();
                ThrowException(ex, response?.StatusCode, audience, detail);
            }
        }
    }
}
