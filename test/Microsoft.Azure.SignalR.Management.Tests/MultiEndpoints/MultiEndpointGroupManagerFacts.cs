// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests.MultiEndpoints
{
    public class MultiEndpointGroupManagerFacts
    {
        private const int Count = 3;
        private const string ConnectionId = "connection_0", groupName = "group_0";
        private readonly ServiceEndpoint[] endpoints = MultiEndpointUtils.GenerateServiceEndpoints(Count);
        public static TheoryData<Expression<Func<IGroupManager, Task>>> MethodExprs = new TheoryData<Expression<Func<IGroupManager, Task>>>
        {
            {gm => gm.AddToGroupAsync(ConnectionId, groupName, default) },
            {gm => gm.RemoveFromGroupAsync(ConnectionId, groupName, default)}
        };

        [Theory]
        [MemberData(nameof(MethodExprs))]
        public async Task TestMethod_Throw(Expression<Func<IGroupManager, Task>> expr)
        {
            //mock IUserGroupManager
            var mocks = Enumerable.Range(0, Count)
               .Select(_ =>
               {
                   var mock = new Mock<IGroupManager>();
                   mock.Setup(expr)
                   .ThrowsAsync(new Exception());
                   return mock;
               });
            //create table
            var table = endpoints.Zip(mocks.Select(mock => mock.Object))
                .ToDictionary(pair => pair.First, pair => pair.Second);
            var router = new EndpointRouterDecorator();
            var gm = new MultiEndpointGroupManager(router, table);

            Func<IGroupManager, Task> func = expr.Compile();
            Task t = func(gm);

            await t.AssertThrowAggregationException(Count);
        }


        [Theory]
        [MemberData(nameof(MethodExprs))]
        public async Task TestMethod_Normal(Expression<Func<IGroupManager, Task>> expr)
        {
            //mock IUserGroupManager
            var mocks = Enumerable.Range(0, Count)
               .Select(_ =>
               {
                   var mock = new Mock<IGroupManager>();
                   mock.Setup(expr)
                   .Returns(Task.CompletedTask);
                   return mock;
               })
               .ToList();
            var dictionary = endpoints.Zip(mocks.Select(mock => mock.Object))
                .ToDictionary(pair => pair.First, pair => pair.Second);
            var routerMock = new Mock<IEndpointRouter>();
            //mock router
            routerMock.Setup(router => router.GetEndpointsForConnection(It.IsAny<string>(), endpoints))
                .Returns(new ServiceEndpoint[] { endpoints[0] });
            routerMock.Setup(router => router.GetEndpointsForGroup(groupName, endpoints))
                .Returns(new ServiceEndpoint[] { endpoints[0], endpoints[1] });
            var mockRouter = routerMock.Object;

            var gm = new MultiEndpointGroupManager(mockRouter, dictionary);
            Func<IGroupManager, Task> func = expr.Compile();

            await func(gm);

            mocks[0].Verify(expr, Times.Once);
            mocks[1].Verify(expr, Times.Never);
            mocks[2].Verify(expr, Times.Never);
        }
    }
}