// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Internal;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests
{
    public class ServiceLifetimeManagerFacts
    {
        private static readonly List<string> TestUsers = new List<string> {"TestUser"};

        private static readonly List<string> TestGroups = new List<string> {"TestGroup"};

        private static readonly string TestMethod = "TestMethod";

        private static readonly object[] TestArgs = new[] {"TestArgs"};

        private static readonly List<string> TestConnectionIds = new List<string> {"connectionId1"};

        private readonly Mock<IServiceConnectionManager<TestHub>> _serviceConnectionManagerMock;

        private readonly ServiceLifetimeManager<TestHub> _serviceLifetimeManager;

        public ServiceLifetimeManagerFacts()
        {
            _serviceConnectionManagerMock = new Mock<IServiceConnectionManager<TestHub>>();
            var clientConnectionManagerMock = new Mock<IClientConnectionManager>();
            _serviceLifetimeManager = new ServiceLifetimeManager<TestHub>(_serviceConnectionManagerMock.Object,
                clientConnectionManagerMock.Object,
                new DefaultHubProtocolResolver(new IHubProtocol[] { new JsonHubProtocol(), new MessagePackHubProtocol() }, NullLogger<DefaultHubProtocolResolver>.Instance),
                NullLogger<ServiceLifetimeManager<TestHub>>.Instance);
        }

        [Fact]
        public async void SendAllAsync()
        {
            _serviceConnectionManagerMock.Setup(m => m.WriteAsync(It.IsAny<BroadcastDataMessage>())).Returns(Task.CompletedTask);
            
            await _serviceLifetimeManager.SendAllAsync(TestMethod, TestArgs);
            _serviceConnectionManagerMock.Verify(m => m.WriteAsync(It.IsAny<BroadcastDataMessage>()), Times.Once);
        }

        [Fact]
        public async void SendAllExceptAsync()
        {
            _serviceConnectionManagerMock.Setup(m => m.WriteAsync(It.IsAny<BroadcastDataMessage>())).Returns(Task.CompletedTask);

            await _serviceLifetimeManager.SendAllExceptAsync(TestMethod, TestArgs, TestConnectionIds);
            _serviceConnectionManagerMock.Verify(m => m.WriteAsync(It.IsAny<BroadcastDataMessage>()), Times.Once);
        }

        [Fact]
        public async void SendConnectionsAsync()
        {
            _serviceConnectionManagerMock.Setup(m => m.WriteAsync(It.IsAny<MultiConnectionDataMessage>())).Returns(Task.CompletedTask);

            await _serviceLifetimeManager.SendConnectionsAsync(TestConnectionIds, TestMethod, TestArgs);
            _serviceConnectionManagerMock.Verify(m => m.WriteAsync(It.IsAny<MultiConnectionDataMessage>()), Times.Once);
        }

        [Fact]
        public async void SendGroupAsync()
        {
            _serviceConnectionManagerMock.Setup(m => m.WriteAsync(It.IsAny<GroupBroadcastDataMessage>())).Returns(Task.CompletedTask);

            await _serviceLifetimeManager.SendGroupAsync(TestGroups[0], TestMethod, TestArgs);
            _serviceConnectionManagerMock.Verify(m => m.WriteAsync(It.IsAny<GroupBroadcastDataMessage>()), Times.Once);
        }

        [Fact]
        public async void SendGroupsAsync()
        {
            _serviceConnectionManagerMock.Setup(m => m.WriteAsync(It.IsAny<MultiGroupBroadcastDataMessage>())).Returns(Task.CompletedTask);

            await _serviceLifetimeManager.SendGroupsAsync(TestGroups, TestMethod, TestArgs);
            _serviceConnectionManagerMock.Verify(m => m.WriteAsync(It.IsAny<MultiGroupBroadcastDataMessage>()), Times.Once);
        }

        [Fact]
        public async void SendGroupExceptAsync()
        {
            _serviceConnectionManagerMock.Setup(m => m.WriteAsync(It.IsAny<GroupBroadcastDataMessage>())).Returns(Task.CompletedTask);

            await _serviceLifetimeManager.SendGroupExceptAsync(TestGroups[0], TestMethod, TestArgs, TestConnectionIds);
            _serviceConnectionManagerMock.Verify(m => m.WriteAsync(It.IsAny<GroupBroadcastDataMessage>()), Times.Once);
        }

        [Fact]
        public async void SendUserAsync()
        {
            _serviceConnectionManagerMock.Setup(m => m.WriteAsync(It.IsAny<UserDataMessage>())).Returns(Task.CompletedTask);

            await _serviceLifetimeManager.SendUserAsync(TestUsers[0], TestMethod, TestArgs);
            _serviceConnectionManagerMock.Verify(m => m.WriteAsync(It.IsAny<UserDataMessage>()), Times.Once);
        }

        [Fact]
        public async void SendUsersAsync()
        {
            _serviceConnectionManagerMock.Setup(m => m.WriteAsync(It.IsAny<MultiUserDataMessage>())).Returns(Task.CompletedTask);

            await _serviceLifetimeManager.SendUsersAsync(TestUsers, TestMethod, TestArgs);
            _serviceConnectionManagerMock.Verify(m => m.WriteAsync(It.IsAny<MultiUserDataMessage>()), Times.Once);
        }

        [Fact]
        public async void AddToGroupAsync()
        {
            _serviceConnectionManagerMock.Setup(m => m.WriteAsync(It.IsAny<JoinGroupMessage>())).Returns(Task.CompletedTask);

            await _serviceLifetimeManager.AddToGroupAsync(TestConnectionIds[0], TestGroups[0]);
            _serviceConnectionManagerMock.Verify(m => m.WriteAsync(It.IsAny<JoinGroupMessage>()), Times.Once);
        }

        [Fact]
        public async void RemoveFromGroupAsync()
        {
            _serviceConnectionManagerMock.Setup(m => m.WriteAsync(It.IsAny<LeaveGroupMessage>())).Returns(Task.CompletedTask);

            await _serviceLifetimeManager.RemoveFromGroupAsync(TestConnectionIds[0], TestGroups[0]);
            _serviceConnectionManagerMock.Verify(m => m.WriteAsync(It.IsAny<LeaveGroupMessage>()), Times.Once);
        }
    }
}
