// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
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
        private const string NullOrEmptyStringErrorMessage = "Argument cannot be null or empty.";

        public RestHubLifetimeManager(ServiceManagerOptions serviceManagerOptions, string hubName)
        {
            _restApiProvider = new RestApiProvider(serviceManagerOptions.ConnectionString, hubName);
        }

        public override Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public override Task OnConnectedAsync(HubConnectionContext connection)
        {
            throw new NotSupportedException();
        }

        public override Task OnDisconnectedAsync(HubConnectionContext connection)
        {
            throw new NotSupportedException();
        }

        public override Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public override Task SendAllAsync(string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(methodName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(methodName));
            }

            var api = _restApiProvider.GetBroadcastEndpoint();
            var request = BuildRequest(api, HttpMethod.Post, methodName, args);
            return CallRestApiAsync(request, cancellationToken);
        }

        public override Task SendAllExceptAsync(string methodName, object[] args, IReadOnlyList<string> excludedConnectionIds, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public override Task SendConnectionAsync(string connectionId, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public override Task SendConnectionsAsync(IReadOnlyList<string> connectionIds, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public override Task SendGroupAsync(string groupName, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(methodName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(methodName));
            }

            if (string.IsNullOrEmpty(groupName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(groupName));
            }

            var api = _restApiProvider.GetSendToGroupEndpoint(groupName);
            var request = BuildRequest(api, HttpMethod.Post, methodName, args);
            return CallRestApiAsync(request, cancellationToken);
        }

        public override Task SendGroupExceptAsync(string groupName, string methodName, object[] args, IReadOnlyList<string> excludedConnectionIds, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public override async Task SendGroupsAsync(IReadOnlyList<string> groupNames, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            Task all = null;

            try
            {
                await (all = Task.WhenAll(from groupName in groupNames
                                          select SendGroupAsync(groupName, methodName, args, cancellationToken)));
            }
            catch
            {
                throw all.Exception;
            }
        }

        public override Task SendUserAsync(string userId, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(methodName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(methodName));
            }

            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(userId));
            }

            var api = _restApiProvider.GetSendToUserEndpoint(userId);
            var request = BuildRequest(api, HttpMethod.Post, methodName, args);
            return CallRestApiAsync(request, cancellationToken);
        }

        public override async Task SendUsersAsync(IReadOnlyList<string> userIds, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            Task all = null;

            try
            {
                await(all = Task.WhenAll(from userId in userIds
                                         select SendUserAsync(userId, methodName, args, cancellationToken)));
            }
            catch
            {
                throw all.Exception;
            }
        }

        public Task UserAddToGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default)
        {
            ValidateUserIdAndGroupName(userId, groupName);

            var api = _restApiProvider.GetUserGroupManagementEndpoint(userId, groupName);
            var request = BuildRequest(api, HttpMethod.Put, null, null);
            return CallRestApiAsync(request, cancellationToken);
        }

        public Task UserRemoveFromGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default)
        {
            ValidateUserIdAndGroupName(userId, groupName);

            var api = _restApiProvider.GetUserGroupManagementEndpoint(userId, groupName);
            var request = BuildRequest(api, HttpMethod.Delete, null, null);
            return CallRestApiAsync(request, cancellationToken);
        }

        private static HttpRequestMessage GenerateHttpRequest(string url, PayloadMessage payload, string tokenString, HttpMethod httpMethod)
        {
            var request = new HttpRequestMessage(httpMethod, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenString);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            return request;
        }

        private static void ThrowExceptionOnResponseFailure(Exception innerException, HttpStatusCode? statusCode, string requestUri, string detail = null)
        {
            switch (statusCode)
            {
                case HttpStatusCode.BadRequest:
                    {
                        throw new AzureSignalRInvalidArgumentException(requestUri, innerException, detail);
                    }
                case HttpStatusCode.Unauthorized:
                    {
                        throw new AzureSignalRUnauthorizedException(requestUri, innerException);
                    }
                case HttpStatusCode.NotFound:
                    {
                        throw new AzureSignalRInaccessibleEndpointException(requestUri, innerException);
                    }
                default:
                    {
                        throw new AzureSignalRRuntimeException(requestUri, innerException);
                    }
            }
        }

        private static HttpRequestMessage BuildRequest(RestApiEndpoint endpoint, HttpMethod httpMethod, string methodName = null, object[] args = null)
        {
            var payload = httpMethod == HttpMethod.Post ? new PayloadMessage { Target = methodName, Arguments = args } : null;
            return GenerateHttpRequest(endpoint.Audience, payload, endpoint.Token, httpMethod);
        }

        private static async Task CallRestApiAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
        {
            var httpClient = HttpClientFactory.CreateClient();
            HttpResponseMessage response = null;
            var detail = "";

            try
            {
                response = await httpClient.SendAsync(request, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                throw new AzureSignalRInaccessibleEndpointException(request.RequestUri.ToString(), ex);
            }

            try
            {
                detail = await response.Content.ReadAsStringAsync();
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                ThrowExceptionOnResponseFailure(ex, response.StatusCode, request.RequestUri.ToString(), detail);
            }
        }

        private static void ValidateUserIdAndGroupName(string userId, string groupName)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(userId));
            }

            if (string.IsNullOrEmpty(groupName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(groupName));
            }
        }
    }
}
