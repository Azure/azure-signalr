// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Azure.SignalR.Common;
using Microsoft.Rest;

namespace Microsoft.Azure.SignalR
{
    public partial class SignalRServiceRestClient
    {
        private readonly string _userAgent;

        public SignalRServiceRestClient(string userAgent, ServiceClientCredentials credentials, HttpClient httpClient, bool disposeHttpClient) : this(credentials, httpClient, disposeHttpClient)
        {
            if (string.IsNullOrWhiteSpace(userAgent))
            {
                throw new ArgumentException($"'{nameof(userAgent)}' cannot be null or whitespace", nameof(userAgent));
            }

            _userAgent = userAgent;
        }

        public async Task<bool> IsServiceHealthy(CancellationToken cancellationToken)
        {
            try
            {
                var healthApi = HealthApi;
                using var response = await healthApi.GetHealthStatusWithHttpMessagesAsync(cancellationToken: cancellationToken);
                return true;
            }
            catch (HttpOperationException e) when ((int)e.Response.StatusCode >= 500 && (int)e.Response.StatusCode < 600)
            {
                return false;
            }
            catch (Exception ex)
            {
                throw ex.WrapAsAzureSignalRException(BaseUri);
            }
        }

        partial void CustomInitialize()
        {
            HttpClient.DefaultRequestHeaders.Add(Constants.AsrsUserAgent, _userAgent);
        }
    }
}