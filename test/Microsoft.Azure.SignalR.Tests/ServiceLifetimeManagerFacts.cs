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

        private static readonly Mock<IServiceConnectionManager<TestHub>> ServiceConnectionManagerMock = new Mock<IServiceConnectionManager<TestHub>>();

        private static readonly Mock<IClientConnectionManager> ClientConnectionManagerMock = new Mock<IClientConnectionManager>();

        private static readonly ServiceLifetimeManager<TestHub> ServiceLifetimeManager = new ServiceLifetimeManager<TestHub>(ServiceConnectionManagerMock.Object,
        ClientConnectionManagerMock.Object,
        new DefaultHubProtocolResolver(new IHubProtocol[] { new JsonHubProtocol(), new MessagePackHubProtocol() }, NullLogger<DefaultHubProtocolResolver>.Instance),
        NullLogger<ServiceLifetimeManager<TestHub>>.Instance
        );

        [Fact]
        public async void SendAllAsync()
        {
            ServiceConnectionManagerMock.Reset();
            ServiceConnectionManagerMock.Setup(m => m.WriteAsync(It.IsAny<BroadcastDataMessage>())).Returns(Task.CompletedTask);
            
            await ServiceLifetimeManager.SendAllAsync(TestMethod, TestArgs);
            ServiceConnectionManagerMock.Verify(m => m.WriteAsync(It.IsAny<BroadcastDataMessage>()), Times.Once);
        }

        [Fact]
        public async void SendAllExceptAsync()
        {
            ServiceConnectionManagerMock.Reset();
            ServiceConnectionManagerMock.Setup(m => m.WriteAsync(It.IsAny<BroadcastDataMessage>())).Returns(Task.CompletedTask);

            await ServiceLifetimeManager.SendAllExceptAsync(TestMethod, TestArgs, TestConnectionIds);
            ServiceConnectionManagerMock.Verify(m => m.WriteAsync(It.IsAny<BroadcastDataMessage>()), Times.Once);
        }

        [Fact]
        public async void SendConnectionsAsync()
        {
            ServiceConnectionManagerMock.Reset();
            ServiceConnectionManagerMock.Setup(m => m.WriteAsync(It.IsAny<MultiConnectionDataMessage>())).Returns(Task.CompletedTask);

            await ServiceLifetimeManager.SendConnectionsAsync(TestConnectionIds, TestMethod, TestArgs);
            ServiceConnectionManagerMock.Verify(m => m.WriteAsync(It.IsAny<MultiConnectionDataMessage>()), Times.Once);
        }

        [Fact]
        public async void SendGroupAsync()
        {
            ServiceConnectionManagerMock.Reset();
            ServiceConnectionManagerMock.Setup(m => m.WriteAsync(It.IsAny<GroupBroadcastDataMessage>())).Returns(Task.CompletedTask);

            await ServiceLifetimeManager.SendGroupAsync(TestGroups[0], TestMethod, TestArgs);
            ServiceConnectionManagerMock.Verify(m => m.WriteAsync(It.IsAny<GroupBroadcastDataMessage>()), Times.Once);
        }

        [Fact]
        public async void SendGroupsAsync()
        {
            ServiceConnectionManagerMock.Reset();
            ServiceConnectionManagerMock.Setup(m => m.WriteAsync(It.IsAny<MultiGroupBroadcastDataMessage>())).Returns(Task.CompletedTask);

            await ServiceLifetimeManager.SendGroupsAsync(TestGroups, TestMethod, TestArgs);
            ServiceConnectionManagerMock.Verify(m => m.WriteAsync(It.IsAny<MultiGroupBroadcastDataMessage>()), Times.Once);
        }

        [Fact]
        public async void SendGroupExceptAsync()
        {
            ServiceConnectionManagerMock.Reset();
            ServiceConnectionManagerMock.Setup(m => m.WriteAsync(It.IsAny<GroupBroadcastDataMessage>())).Returns(Task.CompletedTask);

            await ServiceLifetimeManager.SendGroupExceptAsync(TestGroups[0], TestMethod, TestArgs, TestConnectionIds);
            ServiceConnectionManagerMock.Verify(m => m.WriteAsync(It.IsAny<GroupBroadcastDataMessage>()), Times.Once);
        }

        [Fact]
        public async void SendUserAsync()
        {
            ServiceConnectionManagerMock.Reset();
            ServiceConnectionManagerMock.Setup(m => m.WriteAsync(It.IsAny<UserDataMessage>())).Returns(Task.CompletedTask);

            await ServiceLifetimeManager.SendUserAsync(TestUsers[0], TestMethod, TestArgs);
            ServiceConnectionManagerMock.Verify(m => m.WriteAsync(It.IsAny<UserDataMessage>()), Times.Once);
        }

        [Fact]
        public async void SendUsersAsync()
        {
            ServiceConnectionManagerMock.Reset();
            ServiceConnectionManagerMock.Setup(m => m.WriteAsync(It.IsAny<MultiUserDataMessage>())).Returns(Task.CompletedTask);

            await ServiceLifetimeManager.SendUsersAsync(TestUsers, TestMethod, TestArgs);
            ServiceConnectionManagerMock.Verify(m => m.WriteAsync(It.IsAny<MultiUserDataMessage>()), Times.Once);
        }

        [Fact]
        public async void AddToGroupAsync()
        {
            ServiceConnectionManagerMock.Reset();
            ServiceConnectionManagerMock.Setup(m => m.WriteAsync(It.IsAny<JoinGroupMessage>())).Returns(Task.CompletedTask);

            await ServiceLifetimeManager.AddToGroupAsync(TestConnectionIds[0], TestGroups[0]);
            ServiceConnectionManagerMock.Verify(m => m.WriteAsync(It.IsAny<JoinGroupMessage>()), Times.Once);
        }

        [Fact]
        public async void RemoveFromGroupAsync()
        {
            ServiceConnectionManagerMock.Reset();
            ServiceConnectionManagerMock.Setup(m => m.WriteAsync(It.IsAny<LeaveGroupMessage>())).Returns(Task.CompletedTask);

            await ServiceLifetimeManager.RemoveFromGroupAsync(TestConnectionIds[0], TestGroups[0]);
            ServiceConnectionManagerMock.Verify(m => m.WriteAsync(It.IsAny<LeaveGroupMessage>()), Times.Once);
        }
    }
}
