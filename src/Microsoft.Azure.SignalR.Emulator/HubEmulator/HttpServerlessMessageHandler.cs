// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Serverless.Common;
using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

namespace Microsoft.Azure.SignalR.Emulator.HubEmulator
{
    internal class HttpServerlessMessageHandler<THub> : IUpstreamMessageHandler where THub: Hub
    {
        // We don't support response large than 16M
        private const int MaxAllowedResponseLength = 16 * 1024 * 1024;
        private static readonly byte[] OpenConnectionPayload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(
            new ServerlessProtocol.OpenConnectionMessage { Type = ServerlessProtocol.Constants.OpenConnectionMessageType }
        ));

        private readonly IHttpUpstreamTrigger _httpUpstreamTrigger;
        private readonly string _hubName;
        private readonly ILogger _logger;

        public static readonly MediaTypeHeaderValue JsonMediaType = new MediaTypeHeaderValue(Constants.ContentTypes.JsonContentType);
        public static readonly MediaTypeHeaderValue MessagePackMediaType = new MediaTypeHeaderValue(Constants.ContentTypes.MessagePackContentType);

        public HttpServerlessMessageHandler(IHttpUpstreamTrigger httpUpstreamTrigger, ILogger<HttpServerlessMessageHandler<THub>> logger)
        {
            _httpUpstreamTrigger = httpUpstreamTrigger ?? throw new ArgumentNullException(nameof(httpUpstreamTrigger));
            _logger = logger;
            _hubName = typeof(THub).Name;
        }

        public async Task<ReadOnlySequence<byte>> WriteMessageAsync(HubConnectionContext connectionContext, ServerlessProtocol.InvocationMessage message, CancellationToken token)
        {
            var parameters = new InvokeUpstreamParameters(_hubName, ServerlessProtocol.Constants.Categories.MessageCategory, message.Target);
            if (!_httpUpstreamTrigger.TryGetMatchedUpstreamContext(parameters, out var context))
            {
                // Upstream for this event is not set, ignore the event
                return ReadOnlySequence<byte>.Empty;
            }

            var mediaType = connectionContext.Protocol.Name.Equals("json", StringComparison.OrdinalIgnoreCase) 
                ? JsonMediaType : MessagePackMediaType;
            using (var response = await _httpUpstreamTrigger.GetResponseAsync(
                context,
                connectionContext, message, parameters, mediaType, token))
            {
                response.CheckResponse(parameters, _logger, IsExpectedResponse);
                if (!string.IsNullOrEmpty(message.InvocationId))
                {
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        var contentLength = response.Content.Headers.ContentLength;
                        if (contentLength == 0)
                        {
                            // same as no content.
                            await connectionContext.WriteAsync(CompletionMessage.Empty(message.InvocationId));
                        }
                        else if (contentLength > MaxAllowedResponseLength)
                        {
                            // We don't support response large than 16M, fast fail.
                            await connectionContext.WriteAsync(CompletionMessage.WithError(message.InvocationId, $"Invocation failed, response too large."));
                        }
                        else
                        {
                            var ls = new LimitedStream(MaxAllowedResponseLength);
                            try
                            {
                                await response.Content.CopyToAsync(ls);
                                return new ReadOnlySequence<byte>(ls.ToMemory());
                            }
                            catch (InvalidDataException)
                            {
                                await connectionContext.WriteAsync(CompletionMessage.WithError(message.InvocationId, $"Invocation failed, response too large."));
                            }
                            catch (OperationCanceledException)
                            {
                                await connectionContext.WriteAsync(CompletionMessage.WithError(message.InvocationId, $"Invocation failed, response cancelled."));
                            }
                            catch (Exception ex)
                            {
                                await connectionContext.WriteAsync(CompletionMessage.WithError(message.InvocationId, $"Invocation failed, error: {ex.Message}."));
                            }
                        }
                    }
                    else if (response.StatusCode == HttpStatusCode.NoContent)
                    {
                        await connectionContext.WriteAsync(CompletionMessage.Empty(message.InvocationId));
                    }
                    else
                    {
                        await connectionContext.WriteAsync(CompletionMessage.WithError(message.InvocationId, $"Invocation failed, status code {(int)response.StatusCode}"));
                    }
                }
            }
            return ReadOnlySequence<byte>.Empty;
        }

        public async Task AddClientConnectionAsync(HubConnectionContext connectionContext, CancellationToken token = default)
        {
            var parameters = new InvokeUpstreamParameters(_hubName, ServerlessProtocol.Constants.Categories.ConnectionCategory, ServerlessProtocol.Constants.Events.ConnectedEvent);
            if (!_httpUpstreamTrigger.TryGetMatchedUpstreamContext(parameters, out var context))
            {
                // Upstream for this event is not set, ignore the event
                return;
            }

            using (var response = await _httpUpstreamTrigger.TriggerAsync(
                context,
                connectionContext,
                parameters,
                request =>
                {
                    request.Content = new ByteArrayContent(OpenConnectionPayload);
                    request.Content.Headers.ContentType = JsonMediaType;
                },
                token))
            {
                response.CheckResponse(parameters, _logger, IsExpectedResponse);
            }
        }

        public async Task RemoveClientConnectionAsync(HubConnectionContext connectionContext, string error, CancellationToken token = default)
        {
            var parameters = new InvokeUpstreamParameters(_hubName, ServerlessProtocol.Constants.Categories.ConnectionCategory, ServerlessProtocol.Constants.Events.DisconnectedEvent);
            if (!_httpUpstreamTrigger.TryGetMatchedUpstreamContext(parameters, out var context))
            {
                // Upstream for this event is not set, ignore the event
                return;
            }
            using (var owner = BuildCloseConnectionPayload(error))
            using (var response = await _httpUpstreamTrigger.TriggerAsync(
                context,
                connectionContext,
                parameters,
                request =>
                {
                    request.Content = new ReadOnlyMemoryContent(owner.Memory);
                    request.Content.Headers.ContentType = JsonMediaType;
                },
                token))
            {
                response.CheckResponse(parameters, _logger, IsExpectedResponse);
            }
        }

        private IMemoryOwner<byte> BuildCloseConnectionPayload(string error)
        {
            var writer = MemoryBufferWriter.Get();
            try
            {
                using (var sw = new StreamWriter(writer))
                using (var jw = new JsonTextWriter(sw))
                {
                    jw.WriteStartObject();
                    jw.WritePropertyName(nameof(ServerlessProtocol.CloseConnectionMessage.Type));
                    jw.WriteValue(ServerlessProtocol.Constants.CloseConnectionMessageType);
                    if (error != null)
                    {
                        jw.WritePropertyName(nameof(ServerlessProtocol.CloseConnectionMessage.Error));
                        jw.WriteValue(error);
                    }
                    jw.WriteEndObject();
                    jw.Flush();
                    var result = ExactSizeMemoryPool.Shared.Rent((int)writer.Length);
                    writer.CopyTo(result.Memory.Span);
                    return result;
                }
            }
            finally
            {
                MemoryBufferWriter.Return(writer);
            }
        }

        private bool IsExpectedResponse(HttpResponseMessage response)
        {
            return response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NoContent;
        }
    }
}
