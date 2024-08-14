// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Emulator.Controllers;
using Microsoft.Azure.SignalR.Emulator.HubEmulator;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Microsoft.Azure.SignalR.Emulator.Tests.Controllers
{
    public class SignalRServiceEmulatorWebApiTests
    {
        // Constants
        private const string TestHub = "testHub";
        private const string TestConnectionId = "testConnectionId";
        private const string TestGroup = "testGroup";
        private const string TestUser = "testUser";
        private const string TestApplication = "testApplication";

        private const string MicrosoftErrorCode = "x-ms-error-code";

        private const string Warning_Connection_NotExisted = "Warning.Connection.NotExisted";
        private const string Warning_Group_NotExisted = "Warning.Group.NotExisted";
        private const string Warning_User_NotExisted = "Warning.User.NotExisted";
        private const string Error_Connection_NotExisted = "Error.Connection.NotExisted";
        private const string Info_User_NotInGroup = "Info.User.NotInGroup";

        // Fields
        private readonly Mock<IDynamicHubContextStore> _storeMock;
        private readonly Mock<ILogger<SignalRServiceEmulatorWebApi>> _loggerMock;
        private readonly SignalRServiceEmulatorWebApi _controller;

        public SignalRServiceEmulatorWebApiTests()
        {
            _storeMock = new Mock<IDynamicHubContextStore>();
            _loggerMock = new Mock<ILogger<SignalRServiceEmulatorWebApi>>();
            _controller = new SignalRServiceEmulatorWebApi(_storeMock.Object, _loggerMock.Object);

            HttpContext httpContext = new DefaultHttpContext();
            var controllerContext = new ControllerContext()
            {
                HttpContext = httpContext,
            };

            _controller.ControllerContext = controllerContext;
        }

        // CheckConnectionExistence Tests
        [Fact]
        public void CheckConnectionExistenceValidConnectionReturnsOk()
        {
            // Arrange
            var connectionContext = new DefaultConnectionContext();
            connectionContext.ConnectionId = TestConnectionId;

            var hubConnectionContext = new HubConnectionContext(connectionContext, new HubConnectionContextOptions(), NullLoggerFactory.Instance);

            var connectionStore = new HubConnectionStore();
            connectionStore.Add(hubConnectionContext);

            var lifetimeManagerMock = new Mock<IHubLifetimeManager>();
            lifetimeManagerMock.Setup(l => l.Connections).Returns(connectionStore);

            var dynamicHubContext = new DynamicHubContext(typeof(DynamicHubContext), null, lifetimeManagerMock.Object, null);
            _storeMock.Setup(s => s.TryGetLifetimeContext(It.IsAny<string>(), out dynamicHubContext)).Returns(true);

            // Act
            var result = _controller.CheckConnectionExistence(TestHub, TestConnectionId, TestApplication);

            // Assert
            Assert.IsType<OkResult>(result);
        }

        [Fact]
        public void CheckConnectionExistenceInvalidConnectionReturnsNotFound()
        {
            // arrange
            DynamicHubContext dynamicHubContext = null;
            _storeMock.Setup(s => s.TryGetLifetimeContext(It.IsAny<string>(), out dynamicHubContext)).Returns(false);

            // act
            var result = _controller.CheckConnectionExistence(TestHub, TestConnectionId, TestApplication);

            // assert
            Assert.IsType<NotFoundResult>(result);
            Assert.Equal(Warning_Connection_NotExisted, _controller.Response.Headers[MicrosoftErrorCode]);
        }

        [Fact]
        public void CheckConnectionExistenceInvalidModelStateReturnsBadRequest()
        {
            // arrange
            _controller.ModelState.AddModelError("key", "error");

            // act
            var result = _controller.CheckConnectionExistence(TestHub, TestConnectionId, TestApplication);

            // assert
            Assert.IsType<BadRequestResult>(result);
        }

        // CheckGroupExistence Tests
        [Fact]
        public void CheckGroupExistenceValidConnectionReturnsOk()
        {
            // Arrange
            var groupManager = new GroupManager();
            groupManager.AddConnectionIntoGroup(TestConnectionId, TestGroup);

            var dynamicHubContextMock = new Mock<DynamicHubContext>();
            dynamicHubContextMock.Setup(d => d.UserGroupManager).Returns(groupManager);

            var dynamicHubContext = dynamicHubContextMock.Object;
            _storeMock.Setup(s => s.TryGetLifetimeContext(It.IsAny<string>(), out dynamicHubContext)).Returns(true);

            // Act
            var result = _controller.CheckGroupExistence(TestHub, TestGroup, TestApplication);

            // Assert
            Assert.IsType<OkResult>(result);
        }

        [Fact]
        public void CheckGroupExistenceInvalidConnectionReturnsNotFound()
        {
            // arrange
            DynamicHubContext dynamicHubContext = null;
            _storeMock.Setup(s => s.TryGetLifetimeContext(It.IsAny<string>(), out dynamicHubContext)).Returns(false);

            // act
            var result = _controller.CheckGroupExistence(TestHub, TestGroup, TestApplication);

            // assert
            Assert.IsType<NotFoundResult>(result);
            Assert.Equal(Warning_Group_NotExisted, _controller.Response.Headers[MicrosoftErrorCode]);
        }

        [Fact]
        public void CheckGroupExistenceInvalidModelStateReturnsBadRequest()
        {
            // arrange
            _controller.ModelState.AddModelError("key", "error");

            // act
            var result = _controller.CheckGroupExistence(TestHub, TestGroup, TestApplication);

            // assert
            Assert.IsType<BadRequestResult>(result);
        }

        // CheckUserExistence Tests
        [Fact]
        public void CheckUserExistenceValidConnectionReturnsOk()
        {
            // Arrange
            var connectionContext = new DefaultConnectionContext();

            var hubConnectionContext = new HubConnectionContext(connectionContext, new HubConnectionContextOptions(), NullLoggerFactory.Instance);
            hubConnectionContext.UserIdentifier = TestUser;

            var connectionStore = new HubConnectionStore();
            connectionStore.Add(hubConnectionContext);

            var lifetimeManagerMock = new Mock<IHubLifetimeManager>();
            lifetimeManagerMock.Setup(l => l.Connections).Returns(connectionStore);

            var dynamicHubContext = new DynamicHubContext(typeof(DynamicHubContext), null, lifetimeManagerMock.Object, null);
            _storeMock.Setup(s => s.TryGetLifetimeContext(It.IsAny<string>(), out dynamicHubContext)).Returns(true);

            // Act
            var result = _controller.CheckUserExistence(TestHub, TestUser, TestApplication);

            // Assert
            Assert.IsType<OkResult>(result);
        }

        [Fact]
        public void CheckUserExistenceInvalidConnectionReturnsNotFound()
        {
            // arrange
            DynamicHubContext dynamicHubContext = null;
            _storeMock.Setup(s => s.TryGetLifetimeContext(It.IsAny<string>(), out dynamicHubContext)).Returns(false);

            // act
            var result = _controller.CheckUserExistence(TestHub, TestUser, TestApplication);

            // assert
            Assert.IsType<NotFoundResult>(result);
            Assert.Equal(Warning_User_NotExisted, _controller.Response.Headers[MicrosoftErrorCode]);
        }

        [Fact]
        public void CheckUserExistenceInvalidModelStateReturnsBadRequest()
        {
            // arrange
            _controller.ModelState.AddModelError("key", "error");

            // act
            var result = _controller.CheckUserExistence(TestHub, TestUser, TestApplication);

            // assert
            Assert.IsType<BadRequestResult>(result);
        }

        // RemoveConnectionFromAllGroups Tests
        [Fact]
        public void RemoveConnectionFromAllGroupsValidConnectionReturnsOk()
        {
            // Arrange
            var groupManager = new GroupManager();
            groupManager.AddConnectionIntoGroup(TestConnectionId, TestGroup);

            var dynamicHubContextMock = new Mock<DynamicHubContext>();
            dynamicHubContextMock.Setup(d => d.UserGroupManager).Returns(groupManager);

            var dynamicHubContext = dynamicHubContextMock.Object;
            _storeMock.Setup(s => s.TryGetLifetimeContext(It.IsAny<string>(), out dynamicHubContext)).Returns(true);

            // Act
            var result = _controller.RemoveConnectionFromAllGroups(TestHub, TestConnectionId, TestApplication);

            // Assert
            Assert.IsType<OkResult>(result);
        }

        [Fact]
        public void RemoveConnectionFromAllGroupsInvalidConnectionReturnsOk()
        {
            // arrange
            DynamicHubContext dynamicHubContext = null;
            _storeMock.Setup(s => s.TryGetLifetimeContext(It.IsAny<string>(), out dynamicHubContext)).Returns(false);

            // act
            var result = _controller.RemoveConnectionFromAllGroups(TestHub, TestConnectionId, TestApplication);

            // assert
            Assert.IsType<OkResult>(result);
        }

        [Fact]
        public void RemoveConnectionFromAllGroupsInvalidModelStateReturnsBadRequest()
        {
            // arrange
            _controller.ModelState.AddModelError("key", "error");

            // act
            var result = _controller.RemoveConnectionFromAllGroups(TestHub, TestConnectionId, TestApplication);

            // assert
            Assert.IsType<BadRequestResult>(result);
        }

        // AddConnectionToGroup Tests
        [Fact]
        public void AddConnectionToGroupValidConnectionReturnsOk()
        {
            // Arrange
            var connectionContext = new DefaultConnectionContext();
            connectionContext.ConnectionId = TestConnectionId;

            var hubConnectionContext = new HubConnectionContext(connectionContext, new HubConnectionContextOptions(), NullLoggerFactory.Instance);

            var connectionStore = new HubConnectionStore();
            connectionStore.Add(hubConnectionContext);

            var lifetimeManagerMock = new Mock<IHubLifetimeManager>();
            lifetimeManagerMock.Setup(l => l.Connections).Returns(connectionStore);

            var dynamicHubContext = new DynamicHubContext(typeof(DynamicHubContext), null, lifetimeManagerMock.Object, null);
            _storeMock.Setup(s => s.TryGetLifetimeContext(It.IsAny<string>(), out dynamicHubContext)).Returns(true);

            // Act
            var result = _controller.AddConnectionToGroup(TestHub, TestGroup, TestConnectionId, TestApplication);

            // Assert
            Assert.IsType<OkResult>(result);
        }

        [Fact]
        public void AddConnectionToGroupInvalidConnectionReturnsNotFound()
        {
            // arrange
            DynamicHubContext dynamicHubContext = null;
            _storeMock.Setup(s => s.TryGetLifetimeContext(It.IsAny<string>(), out dynamicHubContext)).Returns(false);

            // act
            var result = _controller.AddConnectionToGroup(TestHub, TestGroup, TestConnectionId, TestApplication);

            // assert
            Assert.IsType<NotFoundResult>(result);
            Assert.Equal(Error_Connection_NotExisted, _controller.Response.Headers[MicrosoftErrorCode]);
        }

        [Fact]
        public void AddConnectionToGroupInvalidModelStateReturnsBadRequest()
        {
            // arrange
            _controller.ModelState.AddModelError("key", "error");

            // act
            var result = _controller.AddConnectionToGroup(TestHub, TestGroup, TestConnectionId, TestApplication);

            // assert
            Assert.IsType<BadRequestResult>(result);
        }

        // RemoveConnectionFromGroup Tests
        [Fact]
        public void RemoveConnectionFromGroupValidConnectionReturnsOk()
        {
            // Arrange
            var connectionContext = new DefaultConnectionContext();
            connectionContext.ConnectionId = TestConnectionId;

            var hubConnectionContext = new HubConnectionContext(connectionContext, new HubConnectionContextOptions(), NullLoggerFactory.Instance);

            var connectionStore = new HubConnectionStore();
            connectionStore.Add(hubConnectionContext);

            var lifetimeManagerMock = new Mock<IHubLifetimeManager>();
            lifetimeManagerMock.Setup(l => l.Connections).Returns(connectionStore);

            var dynamicHubContext = new DynamicHubContext(typeof(DynamicHubContext), null, lifetimeManagerMock.Object, null);
            _storeMock.Setup(s => s.TryGetLifetimeContext(It.IsAny<string>(), out dynamicHubContext)).Returns(true);

            // Act
            var result = _controller.RemoveConnectionFromGroup(TestHub, TestGroup, TestConnectionId, TestApplication);

            // Assert
            Assert.IsType<NotFoundResult>(result);
            Assert.Equal(Error_Connection_NotExisted, _controller.Response.Headers[MicrosoftErrorCode]);
        }

        [Fact]
        public void RemoveConnectionFromGroupInvalidConnectionReturnsNotFound()
        {
            // arrange
            DynamicHubContext dynamicHubContext = null;
            _storeMock.Setup(s => s.TryGetLifetimeContext(It.IsAny<string>(), out dynamicHubContext)).Returns(false);

            // act
            var result = _controller.RemoveConnectionFromGroup(TestHub, TestGroup, TestConnectionId, TestApplication);

            // assert
            Assert.IsType<NotFoundResult>(result);
            Assert.Equal(Error_Connection_NotExisted, _controller.Response.Headers[MicrosoftErrorCode]);
        }

        [Fact]
        public void RemoveConnectionFromGroupInvalidModelStateReturnsBadRequest()
        {
            // arrange
            _controller.ModelState.AddModelError("key", "error");

            // act
            var result = _controller.AddConnectionToGroup(TestHub, TestGroup, TestConnectionId, TestApplication);

            // assert
            Assert.IsType<BadRequestResult>(result);
        }

        // CheckUserExistenceInGroup Tests
        [Fact]
        public void CheckUserExistenceInGroupValidConnectionReturnsOk()
        {
            // Arrange
            var expireAt = System.DateTimeOffset.Now.AddMinutes(10);

            var groupManager = new GroupManager();
            groupManager.AddUserToGroup(TestUser, TestGroup, expireAt);

            var dynamicHubContextMock = new Mock<DynamicHubContext>();
            dynamicHubContextMock.Setup(d => d.UserGroupManager).Returns(groupManager);

            var dynamicHubContext = dynamicHubContextMock.Object;
            _storeMock.Setup(s => s.TryGetLifetimeContext(It.IsAny<string>(), out dynamicHubContext)).Returns(true);

            // Act
            var result = _controller.CheckUserExistenceInGroup(TestHub, TestGroup, TestUser, TestApplication);

            // Assert
            Assert.IsType<OkResult>(result);
        }

        [Fact]
        public void CheckUserExistenceInGroupInvalidConnectionReturnsNotFound()
        {
            // arrange
            DynamicHubContext dynamicHubContext = null;
            _storeMock.Setup(s => s.TryGetLifetimeContext(It.IsAny<string>(), out dynamicHubContext)).Returns(false);

            // act
            var result = _controller.CheckUserExistenceInGroup(TestHub, TestGroup, TestUser, TestApplication);

            // assert
            Assert.IsType<NotFoundResult>(result);
            Assert.Equal(Info_User_NotInGroup, _controller.Response.Headers[MicrosoftErrorCode]);
        }

        [Fact]
        public void CheckUserExistenceInGroupInvalidModelStateReturnsBadRequest()
        {
            // arrange
            _controller.ModelState.AddModelError("key", "error");

            // act
            var result = _controller.CheckUserExistenceInGroup(TestHub, TestGroup, TestUser, TestApplication);

            // assert
            Assert.IsType<BadRequestResult>(result);
        }

    }
}

