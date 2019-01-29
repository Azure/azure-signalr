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
        private const string NullOrEmptyStringErrorMessage = "Argument cannot be null or empty.";

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

        public override Task SendAllAsync(string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrEmpty(methodName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(methodName));
            }

            var api = _restApiProvider.GetBroadcastEndpoint();
            var request = BuildRequest(api, HttpMethod.Post, methodName, args, cancellationToken);
            return CallRestApiAsync(request);
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
            if (String.IsNullOrEmpty(methodName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(methodName));
            }

            if (String.IsNullOrEmpty(groupName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(groupName));
            }

            var api = _restApiProvider.GetSendToGroupEndpoint(groupName);
            var request = BuildRequest(api, HttpMethod.Post, methodName, args, cancellationToken);
            return CallRestApiAsync(request);
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
            if (String.IsNullOrEmpty(methodName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(methodName));
            }

            if (String.IsNullOrEmpty(userId))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(userId));
            }

            var api = _restApiProvider.GetSendToUserEndpoint(userId);
            var request = BuildRequest(api, HttpMethod.Post, methodName, args, cancellationToken);
            return CallRestApiAsync(request);
        }

        public override Task SendUsersAsync(IReadOnlyList<string> userIds, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task UserAddToGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrEmpty(userId))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(userId));
            }

            if (String.IsNullOrEmpty(groupName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(groupName));
            }

            var api = _restApiProvider.GetUserGroupManagementEndpoint(userId, groupName);
            var request = BuildRequest(api, HttpMethod.Put, null, null, cancellationToken);
            return CallRestApiAsync(request);
        }

        public Task UserRemoveFromGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrEmpty(userId))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(userId));
            }

            if (String.IsNullOrEmpty(groupName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(groupName));
            }

            var api = _restApiProvider.GetUserGroupManagementEndpoint(userId, groupName);
            var request = BuildRequest(api, HttpMethod.Delete, null, null, cancellationToken);
            return CallRestApiAsync(request);
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
            switch (statusCode)
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

        private static void ThrowException(Exception innerException, string requestUri, string detail = null)
        {
            switch (innerException)
            {
                case HttpRequestException hrex:
                    {
                        throw new AzureSignalRUnableToAccessException(requestUri, innerException); // not known host
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
            return GenerateHttpRequest(endpoint.Audience, payload, endpoint.Token, HttpMethod.Post);
        }

        private static async Task CallRestApiAsync(HttpRequestMessage request)
        {
            var httpClient = HttpClientFactory.CreateClient();
            HttpResponseMessage response = null;
            string detail = "";

            try
            {
                response = await httpClient.SendAsync(request);
            }
            catch (Exception ex)
            {
                ThrowException(ex, request.RequestUri.ToString()); // not known host
            }

            try
            {
                detail = await response.Content.ReadAsStringAsync();
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                ThrowException(ex, response?.StatusCode, request.RequestUri.ToString(), detail);
            }
            catch (Exception ex)
            {
                ThrowException(ex, request.RequestUri.ToString(), null);
            }
        }
    }
}
