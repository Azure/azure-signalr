// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Tests.Common;
using Moq;
using Moq.Protected;
using Xunit;

#nullable enable

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class RestHubLifetimeManagerFacts
    {
#if NET7_0_OR_GREATER
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly HttpClient _httpClient;
        private readonly string _hubName = "TestHub";
        private readonly string _appName = "TestApp";
        private readonly RestHubLifetimeManager<TestHub> _manager;

        private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;

        public RestHubLifetimeManagerFacts()
        {
            _httpMessageHandlerMock = new Mock<HttpMessageHandler>();

            _httpClient = new HttpClient(_httpMessageHandlerMock.Object);

            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _httpClientFactoryMock
                .Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(_httpClient);

            var restClient = new RestClient(_httpClientFactoryMock.Object);

            _manager = new RestHubLifetimeManager<TestHub>(
                _hubName,
                new(FakeEndpointUtils.GetFakeConnectionString(1).First()),
                _appName,
                restClient,
                new DefaultHubProtocolResolver(new IHubProtocol[]
                {
                    new JsonHubProtocol(),
                    new MessagePackHubProtocol()
                })
            );
        }

        [Fact]
        public async Task InvokeConnectionAsync_NullMethodName_ThrowsArgumentException()
        {
            string? methodName = null;
            var connectionId = "connection1";
            var args = Array.Empty<object>();

            var exception = await Assert.ThrowsAsync<ArgumentException>(
                async () => await _manager.InvokeConnectionAsync<string>(connectionId, methodName!, args));

            Assert.Equal("methodName", exception.ParamName);

            methodName = "";
            exception = await Assert.ThrowsAsync<ArgumentException>(
                async () => await _manager.InvokeConnectionAsync<string>(connectionId, methodName, args));
            Assert.Equal("methodName", exception.ParamName);
        }

        [Fact]
        public async Task InvokeConnectionAsync_NullConnectionId_ThrowsArgumentException()
        {
            var methodName = "testMethod";
            string? connectionId = null;
            var args = Array.Empty<object>();

            var exception = await Assert.ThrowsAsync<ArgumentException>(
                async () => await _manager.InvokeConnectionAsync<string>(connectionId!, methodName, args));

            Assert.Equal("connectionId", exception.ParamName);

            connectionId = "";
            exception = await Assert.ThrowsAsync<ArgumentException>(
                async () => await _manager.InvokeConnectionAsync<string>(connectionId, methodName, args));
            Assert.Equal("connectionId", exception.ParamName);
        }

        [Fact]
        public async Task InvokeConnectionAsync_WithNotFoundResponse_ThrowsHubException()
        {
            // Arrange
            var connectionId = "connection1";
            var methodName = "getError";
            var args = Array.Empty<object>();
            var errorMessage = "Connection does not exist.";

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent(errorMessage)
                });

            // Act & Assert
            var exception = await Assert.ThrowsAsync<AzureSignalRInaccessibleEndpointException>(
                async () => await _manager.InvokeConnectionAsync<string>(connectionId, methodName, args));

        }

        [Fact]
        public async Task InvokeConnectionAsync_WithBadRequestResponse_ThrowsHubException()
        {
            // Arrange
            var connectionId = "connection1";
            var methodName = "getError";
            var args = Array.Empty<object>();
            var errorMessage = "This is a Bad Request.";

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(errorMessage)
                });

            // Act & Assert
            var exception = await Assert.ThrowsAsync<HubException>(
                async () => await _manager.InvokeConnectionAsync<string>(connectionId, methodName, args));
            Assert.Equal(errorMessage, exception.Message);
        }

        [Fact]
        public async Task InvokeConnectionAsync_WithStringResult_ReturnsDeserializedValue()
        {
            // Arrange
            var connectionId = "connection1";
            var methodName = "getUsername";
            var args = new object?[] { 42, "test-param", true };
            var expectedResult = "John Doe";

            // Build a CompletionMessage carrying the string result
            var completion = new CompletionMessage(
                invocationId: "1234",
                error: null,
                result: expectedResult,
                hasResult: true);

            // Serialize to SignalR JSON frame (with record separator)
            var protocol = new JsonHubProtocol();
            var payloadBytes = protocol.GetMessageBytes(completion).ToArray();

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                {
                    var response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(payloadBytes),
                    };

                    // Protocol header expected by InvokeConnectionAsync
                    response.Headers.Add(Constants.Headers.AsrsManagementSDKClientInvocationProtocol, protocol.Name);
                    response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

                    return response;
                });

            // Act
            var result = await _manager.InvokeConnectionAsync<string>(connectionId, methodName, args);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public async Task InvokeConnectionAsync_WithComplexObjectResult_ReturnsDeserializedObject()
        {
            // Arrange
            var connectionId = "connection1";
            var methodName = "getUserProfile";
            var args = new object?[] { "userId123", new { filter = "personal" } };

            var expectedProfile = new UserProfile
            {
                id = 123,
                name = "Jane Doe",
                active = true,
                roles = new[] { "user", "admin" },
            };

            var completion = new CompletionMessage(
                invocationId: "1234",
                error: null,
                result: expectedProfile,
                hasResult: true);

            var protocol = new JsonHubProtocol();
            var payloadBytes = protocol.GetMessageBytes(completion).ToArray();

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                {
                    var response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(payloadBytes),
                    };

                    response.Headers.Add(Constants.Headers.AsrsManagementSDKClientInvocationProtocol, protocol.Name);
                    response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

                    return response;
                });

            // Act
            var result = await _manager.InvokeConnectionAsync<UserProfile>(connectionId, methodName, args);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedProfile.id, result.id);
            Assert.Equal(expectedProfile.name, result.name);
            Assert.Equal(expectedProfile.active, result.active);
            Assert.Equal(expectedProfile.roles.Length, result.roles.Length);
            Assert.Contains("admin", result.roles);
        }

        [Fact]
        public async Task InvokeConnectionAsync_WithMissingProtocolHeader_ThrowsHubException()
        {
            // Arrange
            var connectionId = "connection1";
            var methodName = "getData";
            var args = Array.Empty<object?>();

            var protocol = new JsonHubProtocol();
            var completion = new CompletionMessage("1234", null, "value", hasResult: true);
            var payloadBytes = protocol.GetMessageBytes(completion).ToArray();

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(payloadBytes),
                    });

            // Act & Assert
            var ex = await Assert.ThrowsAsync<HubException>(
                () => _manager.InvokeConnectionAsync<string>(connectionId, methodName, args));

            Assert.Equal("Response is missing protocol header.", ex.Message);
        }

        [Fact]
        public async Task InvokeConnectionAsync_WithEmptyPayload_ThrowsHubException()
        {
            // Arrange
            var connectionId = "connection1";
            var methodName = "getData";
            var args = Array.Empty<object?>();

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                {
                    var response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(Array.Empty<byte>()),
                    };
                    response.Headers.Add(Constants.Headers.AsrsManagementSDKClientInvocationProtocol, "json");
                    response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                    return response;
                });

            // Act & Assert
            var ex = await Assert.ThrowsAsync<HubException>(
                () => _manager.InvokeConnectionAsync<string>(connectionId, methodName, args));

            Assert.Equal("Response payload is empty.", ex.Message);
        }
#endif

        public class TestHub : Hub { }

        public class UserProfile
        {
            public int id { get; set; }
            public string name { get; set; } = string.Empty;
            public bool active { get; set; }
            public string[] roles { get; set; } = Array.Empty<string>();
        }

        private sealed class DefaultHubProtocolResolver : IHubProtocolResolver
        {

            private readonly List<IHubProtocol> _hubProtocols;
            private readonly Dictionary<string, IHubProtocol> _availableProtocols;

            public IReadOnlyList<IHubProtocol> AllProtocols => _hubProtocols;

            public DefaultHubProtocolResolver(IEnumerable<IHubProtocol> availableProtocols)
            {
                _availableProtocols = new Dictionary<string, IHubProtocol>(StringComparer.OrdinalIgnoreCase);

                // We might get duplicates in _hubProtocols, but we're going to check it and overwrite in just a sec.
                _hubProtocols = availableProtocols.ToList();
                foreach (var protocol in _hubProtocols)
                {
                    _availableProtocols[protocol.Name] = protocol;
                }
            }

            public IHubProtocol? GetProtocol(string protocolName, IReadOnlyList<string>? supportedProtocols)
            {
                protocolName = protocolName ?? throw new ArgumentNullException(nameof(protocolName));

                if (_availableProtocols.TryGetValue(protocolName, out var protocol) && (supportedProtocols == null || supportedProtocols.Contains(protocolName, StringComparer.OrdinalIgnoreCase)))
                {
                    return protocol;
                }

                // null result indicates protocol is not supported
                // result will be validated by the caller
                return null;
            }
        }
    }
}
