// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
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
                restClient
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
        public async Task InvokeConnectionAsync_WithStringResult_ReturnsDeserializedValue()
        {
            // Arrange
            var connectionId = "connection1";
            var methodName = "getUsername";
            var args = new object?[] { 42, "test-param", true };
            var expectedResult = "John Doe";

            var jsonResponse = $"{{\"result\":\"{expectedResult}\"}}";

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(jsonResponse)
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

            var jsonResponse = @"{""result"":{""id"":123,""name"":""Jane Doe"",""active"":true,""roles"":[""user"",""admin""]}}";

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(jsonResponse)
                });

            // Act
            var result = await _manager.InvokeConnectionAsync<UserProfile>(connectionId, methodName, args);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(123, result.id);
            Assert.Equal("Jane Doe", result.name);
            Assert.True(result.active);
            Assert.Equal(2, result.roles.Length);
            Assert.Contains("admin", result.roles);
        }

        [Fact]
        public async Task InvokeConnectionAsync_WithErrorResponse_ThrowsHubException()
        {
            // Arrange
            var connectionId = "connection1";
            var methodName = "getError";
            var args = Array.Empty<object>();
            var errorMessage = "Connection does not exist";

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
        public async Task InvokeConnectionAsync_WithMissingResultNode_ThrowsHubException()
        {
            // Arrange
            var connectionId = "connection1";
            var methodName = "getIncompleteData";
            var args = Array.Empty<object>();

            // JSON missing the required result node
            var incompleteJsonResponse = "{\"jsonObject\":{}}";

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(incompleteJsonResponse)
                });

            // Act & Assert
            var exception = await Assert.ThrowsAsync<HubException>(
                async () => await _manager.InvokeConnectionAsync<string>(connectionId, methodName, args));

            Assert.Contains("Result not found in JSON response", exception.Message);
        }

        [Fact]
        public async Task InvokeConnectionAsync_WithMissingJsonObjectNode_ThrowsHubException()
        {
            // Arrange
            var connectionId = "connection1";
            var methodName = "getIncompleteData";
            var args = Array.Empty<object>();

            // JSON missing the required jsonObject node
            var incompleteJsonResponse = "{}";

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(incompleteJsonResponse)
                });

            // Act & Assert
            var exception = await Assert.ThrowsAsync<HubException>(
                async () => await _manager.InvokeConnectionAsync<string>(connectionId, methodName, args));

            Assert.Contains("Result not found in JSON response", exception.Message);
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
    }
}
