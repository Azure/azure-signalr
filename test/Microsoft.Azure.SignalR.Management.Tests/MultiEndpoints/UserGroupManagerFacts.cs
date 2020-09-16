// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests.MultiEndpoints
{
    public class UserGroupManagerFacts
    {
        private const string UserName = "user1";
        private const string GroupName = "group1";
        private const int Count = 3;
        private static readonly TimeSpan _ttl = TimeSpan.FromMinutes(1);
        private readonly ServiceEndpoint[] _endpoints = Enumerable.Range(0, Count)
            .Select(id => new ServiceEndpoint($"Endpoint=http://endpoint{id};AccessKey=accessKey;Version=1.0;")).ToArray();

        public static readonly TheoryData<Expression<Func<IUserGroupManager, Task>>> VoidMethodExprs = new TheoryData<Expression<Func<IUserGroupManager, Task>>>
        {
            {m=>m.AddToGroupAsync(UserName,GroupName,default) },
            {m=>m.AddToGroupAsync(UserName,GroupName,_ttl,default) },
            {m=>m.RemoveFromGroupAsync(UserName,GroupName,default) },
            {m=>m.RemoveFromAllGroupsAsync(UserName,default) }
        };

        [Theory]
        [MemberData(nameof(VoidMethodExprs))]
        public async Task Normal_Theories(Expression<Func<IUserGroupManager, Task>> expr)
        {
            var mocks = Enumerable.Range(0, Count)
                .Select(_ =>
                {
                    var mock = new Mock<IUserGroupManager>();
                    mock.Setup(expr).Returns(Task.CompletedTask);
                    return mock;
                })
                .ToList();
            var managers = mocks.Select(mock => mock.Object);
            Func<IUserGroupManager, Task> func = expr.Compile();
            var multiEndpointManager = CreateMeUserGroupManager(managers);

            await func(multiEndpointManager);

            //only endpoint[1] is routed
            mocks[0].Verify(expr, Times.Once);
            for (int i = 1; i < mocks.Count; i++)
            {
                mocks[i].Verify(expr, Times.Never);
            }
        }

        [Theory]
        [MemberData(nameof(VoidMethodExprs))]
        public async Task Throw_Theories(Expression<Func<IUserGroupManager, Task>> expr)
        {
            var mocks = Enumerable.Range(0, Count)
                .Select(_ =>
                {
                    var mock = new Mock<IUserGroupManager>();
                    mock.Setup(expr).ThrowsAsync(new Exception());
                    return mock;
                })
                .ToList();
            Func<IUserGroupManager, Task> func = expr.Compile();
            var helper = new AggreExcpVerificationHelper();
            var managers = mocks.Select(mock => mock.Object);

            var multiEndpointManager = CreateMeUserGroupManager(managers);
            Task t = func(multiEndpointManager);

            await helper.AssertIsAggreExp(1, t); //only one endpoint is routed to according to mock router
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(false, false)]
        [InlineData(true, true, false)]
        [InlineData(false, false, false)]
        public async Task IsUserInGroup_AllCallsSuccess_Theories(bool expected, params bool[] existenceFromEachEndpoint)
        {
            var mocks = existenceFromEachEndpoint
                .Select(existence =>
                {
                    var mock = new Mock<IUserGroupManager>();
                    mock.Setup(m => m.IsUserInGroup(UserName, GroupName, default))
                    .ReturnsAsync(existence);
                    return mock;
                });
            var managers = from mock in mocks select mock.Object;
            Dictionary<ServiceEndpoint, IUserGroupManager> table = _endpoints
                .Zip(managers)
                .ToDictionary(pair => pair.First, pair => pair.Second);
            IEndpointRouter router = new EndpointRouterDecorator();
            var meUserGroupManger = new MultiEndpointUserGroupManager(router, table);

            var actual = await meUserGroupManger.IsUserInGroup(UserName, GroupName, default);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void IsUserInGroup_ReturnOnceHitFact()
        {
            IUserGroupManager successManager, delayManager, errorManager;
            var token = new CancellationTokenSource().Token;
            successManager = GetMockManagerWithIsUserInGroup(true, token);
            delayManager = GetMockManagerWithIsUserInGroup_Delay(false, token);
            errorManager = GetMockManagerWithIsUserInGroup_Throw(token);
            var managers = new IUserGroupManager[] { successManager, delayManager, errorManager };
            var table = _endpoints
                .Zip(managers)
                .ToDictionary(pair => pair.First, pair => pair.Second);
            var router = new EndpointRouterDecorator();
            var multiEndpointManager = new MultiEndpointUserGroupManager(router, table);
            var t = multiEndpointManager.IsUserInGroup(UserName, GroupName, token);
            bool finished = t.Wait(100); //make sure the whole task completes without waiting for the delay task
            bool existence = t.Result;

            Assert.True(finished);
            Assert.True(existence);
        }

        [Fact]
        public async void IsUserInGroup_ThrowFact()
        {
            IUserGroupManager timeoutManager, notFoundManager;
            var token = new CancellationTokenSource().Token;
            timeoutManager = GetMockManagerWithIsUserInGroup_Throw(token, new HttpRequestException());
            notFoundManager = GetMockManagerWithIsUserInGroup_Delay(false, token);
            var managers = new IUserGroupManager[] { timeoutManager, notFoundManager };
            var table = _endpoints
                .Zip(managers)
                .ToDictionary(pair => pair.First, pair => pair.Second);
            var router = new EndpointRouterDecorator();
            var multiEndpointManager = new MultiEndpointUserGroupManager(router, table);

            Task<bool> action() => multiEndpointManager.IsUserInGroup(UserName, GroupName, token);

            var aggrExp = await Assert.ThrowsAsync<AggregateException>(action);
            Assert.IsType<HttpRequestException>(aggrExp.InnerExceptions.Single());
        }

        private IEndpointRouter CreateMockRouter()
        {
            var routerMock = new Mock<IEndpointRouter>();
            routerMock.Setup(router => router.GetEndpointsForUser(UserName, _endpoints))
                .Returns(new ServiceEndpoint[] { _endpoints[0] });
            routerMock.Setup(router => router.GetEndpointsForGroup(GroupName, _endpoints))
                .Returns(new ServiceEndpoint[] { _endpoints[0], _endpoints[1] });
            return routerMock.Object;
        }

        private MultiEndpointUserGroupManager CreateMeUserGroupManager(IEnumerable<IUserGroupManager> managers)
        {
            Dictionary<ServiceEndpoint, IUserGroupManager> table = _endpoints.Zip(managers).ToDictionary(pair => pair.First, pair => pair.Second);
            var mockRoute = CreateMockRouter();
            return new MultiEndpointUserGroupManager(mockRoute, table);
        }

        private IUserGroupManager GetMockManagerWithIsUserInGroup(bool returnResult, CancellationToken token)
        {
            var mock = new Mock<IUserGroupManager>();
            mock.Setup(m => m.IsUserInGroup(UserName, GroupName, token))
                .ReturnsAsync(returnResult);
            return mock.Object;
        }

        private IUserGroupManager GetMockManagerWithIsUserInGroup_Throw(CancellationToken token, Exception e = null)
        {
            if (e == null)
            {
                e = new Exception();
            }
            var mock = new Mock<IUserGroupManager>();
            mock.Setup(m => m.IsUserInGroup(UserName, GroupName, token))
                .ThrowsAsync(e);
            return mock.Object;
        }

        private IUserGroupManager GetMockManagerWithIsUserInGroup_Delay(bool returnResult, CancellationToken token)
        {
            var mock = new Mock<IUserGroupManager>();
            mock.Setup(m => m.IsUserInGroup(UserName, GroupName, token))
                .ReturnsAsync(() =>
                {
                    Task.Delay(1000);
                    return returnResult;
                });
            return mock.Object;
        }
    }
}