// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Serverless.Common;

namespace Microsoft.Azure.SignalR.Emulator
{
    internal static class HttpUpstreamTriggerExtensions
    {
        public static async Task<HttpResponseMessage> GetResponseAsync(this IHttpUpstreamTrigger httpUpstream, UpstreamContext context, HubConnectionContext connectionContext, ServerlessProtocol.InvocationMessage message, InvokeUpstreamParameters parameters, MediaTypeHeaderValue mediaType = null, CancellationToken token = default)
        {
            var upstream = connectionContext.Features.Get<IHttpUpstreamPropertiesFeature>();
            if (upstream == null)
            {
                // defensive code, should not happen
                throw new InvalidOperationException("IHttpUpstreamPropertiesFeature expected");
            }

            if (message.Payload.IsSingleSegment)
            {
                return await httpUpstream.TriggerAsync(
                    context,
                    upstream,
                    parameters,
                    request =>
                    {
                        request.Content = new ReadOnlyMemoryContent(message.Payload.First);
                        request.Content.Headers.ContentType = mediaType;
                    },
                    token);
            }

            using (var owner = ExactSizeMemoryPool.Shared.Rent((int)message.Payload.Length))
            {
                message.Payload.CopyTo(owner.Memory.Span);

                return await httpUpstream.TriggerAsync(
                    context,
                    upstream,
                    parameters,
                    request =>
                    {
                        request.Content = new ReadOnlyMemoryContent(owner.Memory);
                        request.Content.Headers.ContentType = mediaType;
                    },
                    token);
            }
        }

        public static Task<HttpResponseMessage> TriggerAsync(this IHttpUpstreamTrigger trigger, UpstreamContext context, HubConnectionContext connectionContext, InvokeUpstreamParameters parameters, Action<HttpRequestMessage> configureRequest = null, CancellationToken token = default)
        {
            var upstreamFeatures = connectionContext.Features.Get<IHttpUpstreamPropertiesFeature>();
            if (upstreamFeatures == null)
            {
                // defensive code, should not happen
                throw new InvalidOperationException("IHttpUpstreamPropertiesFeature expected");
            }

            return trigger.TriggerAsync(context, upstreamFeatures, parameters, configureRequest, token);
        }
    }
}
