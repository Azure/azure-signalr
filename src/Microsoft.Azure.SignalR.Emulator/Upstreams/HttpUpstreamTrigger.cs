// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Azure.SignalR.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Emulator
{
    internal class HttpUpstreamTrigger : IHttpUpstreamTrigger
    {
        private readonly IOptionsMonitor<UpstreamOptions> _optionsMonitor;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger _logger;

        public HttpUpstreamTrigger(IOptionsMonitor<UpstreamOptions> optionsMonitor, IHttpClientFactory httpClientFactory, ILogger<HttpUpstreamTrigger> logger)
        {
            _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<HttpResponseMessage> TriggerAsync(UpstreamContext context, IHttpUpstreamPropertiesFeature upstreamProperties, InvokeUpstreamParameters parameters, Action<HttpRequestMessage> configureRequest = null, CancellationToken token = default)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, context.Uri);

            request.Headers.Add(Constants.Headers.AsrsConnectionIdHeader, upstreamProperties.ConnectionId);
            request.Headers.Add(Constants.Headers.AsrsHubNameHeader, parameters.Hub);
            request.Headers.Add(Constants.Headers.AsrsCategory, parameters.Category);
            request.Headers.Add(Constants.Headers.AsrsEvent, parameters.Event);

            // userId can be null when we allow anonymous login
            if (!string.IsNullOrEmpty(upstreamProperties.UserIdentifier))
            {
                request.Headers.Add(Constants.Headers.AsrsUserId, upstreamProperties.UserIdentifier);
            }

            if (upstreamProperties.ClaimStrings?.Count > 0)
            {
                request.Headers.Add(Constants.Headers.AsrsUserClaims, upstreamProperties.ClaimStrings);
            }

            if (!string.IsNullOrEmpty(upstreamProperties.QueryString))
            {
                request.Headers.Add(Constants.Headers.AsrsClientQueryString, upstreamProperties.QueryString);
            }

            var signature = upstreamProperties.GetSignatures(new string[] { AppBuilderExtensions.AccessKey });
            if (signature != null)
            {
                request.Headers.Add(Constants.Headers.AsrsSignature, signature);
            }

            configureRequest?.Invoke(request);

            return await SafeAuthAndSendAsync(request, parameters.ToString(), token);
        }

        public bool TryGetMatchedUpstreamContext(InvokeUpstreamParameters parameters, out UpstreamContext upstreamContext)
        {
            upstreamContext = null;
            var templates = _optionsMonitor.CurrentValue.Templates;
            if (templates == null)
            {
                return false;
            }

            // order matters
            foreach (var templateItem in templates)
            {
                if (templateItem.IsMatch(parameters))
                {
                    upstreamContext = new UpstreamContext();
                    upstreamContext.Uri = Utils.GetUpstreamUrl(templateItem.UrlTemplate, parameters.Hub, parameters.Category, parameters.Event);
                    return true;
                }
            }

            return false;
        }

        private async Task<HttpResponseMessage> SafeAuthAndSendAsync(HttpRequestMessage request, string operationName, CancellationToken token)
        {
            try
            {
                return await SendAsync(request, operationName, token);
            }
            catch (Exception e)
            {
                _logger.LogWarning($"Failed to write message during operation {operationName}: {e}");
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }
        }

        private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, string operationName, CancellationToken token)
        {
            using (var client = _httpClientFactory.CreateClient())
            {
                var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
                if (response.StatusCode == HttpStatusCode.InternalServerError)
                {
                    // log 500 here as outer 500 is mixed with exception
                    _logger.LogWarning($"Sending message during operation {operationName} got unexpected response with status code {500}.");
                }

                return response;
            }
        }
    }
}
