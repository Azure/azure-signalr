// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class WebsocketsHubLifetimeManagerFacts
    {
        private readonly Mock<IServiceConnectionManager<TestHub>> _serviceConnectionManagerMock;
        private readonly DefaultHubProtocolResolver _protocolResolver;
        private readonly Mock<IOptions<HubOptions>> _globalHubOptionsMock;
        private readonly Mock<IOptions<HubOptions<TestHub>>> _hubOptionsMock;
        private readonly Mock<ILoggerFactory> _loggerFactoryMock;
        private readonly Mock<IOptions<ServiceManagerOptions>> _serviceManagerOptionsMock;
        private readonly Mock<IClientInvocationManager> _clientInvocationManagerMock;
        private readonly Mock<IServerNameProvider> _serverNameProviderMock;
        private readonly string _hubName = "TestHub";

        public WebsocketsHubLifetimeManagerFacts()
        {
            _serviceConnectionManagerMock = new Mock<IServiceConnectionManager<TestHub>>();
            _protocolResolver = new DefaultHubProtocolResolver([new JsonHubProtocol(), new MessagePackHubProtocol()]);
            _globalHubOptionsMock = new Mock<IOptions<HubOptions>>();
            _hubOptionsMock = new Mock<IOptions<HubOptions<TestHub>>>();
            _loggerFactoryMock = new Mock<ILoggerFactory>();
            _loggerFactoryMock.Setup(l => l.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());
            _serviceManagerOptionsMock = new Mock<IOptions<ServiceManagerOptions>>();
            _clientInvocationManagerMock = new Mock<IClientInvocationManager>();
            _serverNameProviderMock = new Mock<IServerNameProvider>();
            _serverNameProviderMock.Setup(s => s.GetName()).Returns("TestServer");

            _globalHubOptionsMock.SetupGet(o => o.Value).Returns(new HubOptions { SupportedProtocols = new List<string> { "json", "messagepack" } });
            _hubOptionsMock.SetupGet(o => o.Value).Returns(new HubOptions<TestHub> { SupportedProtocols = new List<string> { "json", "messagepack" } });
            _serviceManagerOptionsMock.SetupGet(o => o.Value).Returns(new ServiceManagerOptions { EnableMessageTracing = true });
        }

        [Fact]
        public void Constructor_ShouldInitialize_WhenDependenciesAreValid()
        {
            // Act
            var manager = new WebSocketsHubLifetimeManager<TestHub>(
                _serviceConnectionManagerMock.Object,
                _protocolResolver,
                _globalHubOptionsMock.Object,
                _hubOptionsMock.Object,
                _loggerFactoryMock.Object,
                _serviceManagerOptionsMock.Object,
                _clientInvocationManagerMock.Object,
                _serverNameProviderMock.Object,
                _hubName
            );

            // Assert
            Assert.NotNull(manager);
            _serverNameProviderMock.Verify(s => s.GetName(), Times.Once);
        }

#if NET7_0_OR_GREATER
        [Fact]
        public async Task InvokeConnectionAsync_ShouldInvokeMethod_WhenArgumentsAreValid()
        {
            // Arrange
            var manager = new WebSocketsHubLifetimeManager<TestHub>(
                _serviceConnectionManagerMock.Object,
                _protocolResolver,
                _globalHubOptionsMock.Object,
                _hubOptionsMock.Object,
                _loggerFactoryMock.Object,
                _serviceManagerOptionsMock.Object,
                _clientInvocationManagerMock.Object,
                _serverNameProviderMock.Object,
                _hubName
            );

            var connectionId = "test-connection-id";
            var methodName = "TestMethod";
            var args = new object[] { "arg1", 2 };
            var cancellationToken = new CancellationTokenSource().Token;
            var invocationId = "test-invocation-id";
            var expectedResult = "result";
            var _hub = "TestHub";

            _clientInvocationManagerMock.Setup(m => m.Caller.GenerateInvocationId(connectionId)).Returns(invocationId);
            _clientInvocationManagerMock.Setup(m => m.Caller.AddInvocation<string>(_hub, connectionId, invocationId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await manager.InvokeConnectionAsync<string>(connectionId, methodName, args, cancellationToken);

            // Assert
            Assert.Equal(expectedResult, result);
            _clientInvocationManagerMock.Verify(m => m.Caller.GenerateInvocationId(connectionId), Times.Once);
            _clientInvocationManagerMock.Verify(m => m.Caller.AddInvocation<string>(_hub, connectionId, invocationId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InvokeConnectionAsync_ShouldThrowArgumentNullException_WhenConnectionIdIsInvalid()
        {
            // Arrange
            var manager = new WebSocketsHubLifetimeManager<TestHub>(
                _serviceConnectionManagerMock.Object,
                _protocolResolver,
                _globalHubOptionsMock.Object,
                _hubOptionsMock.Object,
                _loggerFactoryMock.Object,
                _serviceManagerOptionsMock.Object,
                _clientInvocationManagerMock.Object,
                _serverNameProviderMock.Object,
                _hubName
            );

            string invalidConnectionId = null!;
            var methodName = "TestMethod";
            var args = new object[] { "arg1", 2 };
            var cancellationToken = new CancellationTokenSource().Token;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => manager.InvokeConnectionAsync<string>(invalidConnectionId, methodName, args, cancellationToken));
        }

        [Fact]
        public async Task InvokeConnectionAsync_ShouldThrowArgumentNullException_WhenMethodNameIsInvalid()
        {
            // Arrange
            var manager = new WebSocketsHubLifetimeManager<TestHub>(
                _serviceConnectionManagerMock.Object,
                _protocolResolver,
                _globalHubOptionsMock.Object,
                _hubOptionsMock.Object,
                _loggerFactoryMock.Object,
                _serviceManagerOptionsMock.Object,
                _clientInvocationManagerMock.Object,
                _serverNameProviderMock.Object,
                _hubName
            );

            var connectionId = "test-connection-id";
            string invalidMethodName = null!;
            var args = new object[] { "arg1", 2 };
            var cancellationToken = new CancellationTokenSource().Token;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => manager.InvokeConnectionAsync<string>(connectionId, invalidMethodName, args, cancellationToken));
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

            public IHubProtocol GetProtocol(string protocolName, IReadOnlyList<string> supportedProtocols)
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
#endif

    }

    // Dummy Hub for testing
    public class TestHub : Hub { }

}
