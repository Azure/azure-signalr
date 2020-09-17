// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests.MultiEndpoints
{
    public class HubClientsFacts
    {
        private const int EndpointCount = 3;
        private const int IdCount = 3;
        private const int GroupCount = 3;
        private const int UserCount = 3;
        private const string Method = "method";
        private static readonly object[] args = new object[0];
        private static readonly string[] connectionIds = Enumerable.Range(0, IdCount)
            .Select(id => $"connection_{id}").ToArray();
        private static readonly string[] groupNames = Enumerable.Range(0, GroupCount)
            .Select(id => $"group_{id}").ToArray();
        private static readonly string[] userIds = Enumerable.Range(0, UserCount)
    .Select(id => $"user_{id}").ToArray();
        private static readonly CancellationToken token = default;
        private static readonly ServiceEndpoint[] _endpoints = MultiEndpointUtils.GenerateServiceEndpoints(EndpointCount);

        [Fact]
        public async void All_Normal_Fact()
        {
            var hubClients_ClientProxies = Enumerable.Range(0, EndpointCount)
                .Select(_ =>
                {
                    var IHubClientsMock = new Mock<IHubClients>();
                    var IClientProxyMock = IHubClientsMock.As<IHubClients<IClientProxy>>();
                    IClientProxyMock.SetupGet(proxy => proxy.All);
                    IClientProxyMock.Setup(
                        proxy => proxy.All.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()));
                    return (IHubClientsMock, IClientProxyMock);
                }).ToList();
            var router = GetMockRouter();
            var multiEndpointHubClients = GetMultiEndpointHubClients(hubClients_ClientProxies, router);

            await multiEndpointHubClients.All.SendCoreAsync(Method, args, token);

            foreach (var (IHubClientsMock, IClientProxyMock) in hubClients_ClientProxies)
            {
                IClientProxyMock.Verify(proxy => proxy.All);
                IClientProxyMock.Verify(proxy => proxy.All.SendCoreAsync(Method, args, token));
            }
        }

        [Fact]
        public async void All_Throw_Fact()
        {
            var hubClients_ClientProxies = Enumerable.Range(0, EndpointCount)
                .Select(_ =>
                {
                    var IHubClientsMock = new Mock<IHubClients>();
                    var IClientProxyMock = IHubClientsMock.As<IHubClients<IClientProxy>>();
                    IClientProxyMock.SetupGet(proxy => proxy.All);
                    IClientProxyMock.Setup(
                        proxy => proxy.All.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception());
                    return (IHubClientsMock, IClientProxyMock);
                }).ToList();
            var router = GetMockRouter();
            var multiEndpointHubClients = GetMultiEndpointHubClients(hubClients_ClientProxies, router);

            Task t = multiEndpointHubClients.All.SendCoreAsync(Method, args, token);

            await t.AssertThrowAggregationException(EndpointCount);
        }

        public static readonly TheoryData<Expression<Func<IHubClients<IClientProxy>, IClientProxy>>> MethodsTestedWithDefaultRouter = new TheoryData<Expression<Func<IHubClients<IClientProxy>, IClientProxy>>>
        {
            {c=>c.AllExcept(connectionIds) },
            {c=>c.Client(connectionIds[0]) },
            {c=>c.Clients(connectionIds)},
            {c=>c.Group(groupNames[0]) },
            {c=>c.Groups(groupNames) },
            {c=>c.User(userIds[0]) },
            {c=>c.Users(userIds)}
        };

        [Theory]
        [MemberData(nameof(MethodsTestedWithDefaultRouter))]
        public async void WithDefaultRouter_Normal_Theories(Expression<Func<IHubClients<IClientProxy>, IClientProxy>> expr)
        {
            var sendExpr = CreateSendExpr(expr);
            var func = expr.Compile();
            var router = new EndpointRouterDecorator();
            var hubClients_ClientProxies = GetHubClients_Client_Proxies(expr);
            var multiEndpointHubClients = GetMultiEndpointHubClients(hubClients_ClientProxies, router);

            await func(multiEndpointHubClients).SendCoreAsync(Method, args, token);

            foreach (var (IHubClientsMock, IClientProxyMock) in hubClients_ClientProxies)
            {
                IClientProxyMock.Verify(sendExpr, Times.Once);
            }
        }

        [Theory]
        [MemberData(nameof(MethodsTestedWithDefaultRouter))]
        public async void WithDefaultRouter_Throw_Theories(Expression<Func<IHubClients<IClientProxy>, IClientProxy>> expr)
        {
            var sendExpr = CreateSendExpr(expr);
            var func = expr.Compile();
            var router = new EndpointRouterDecorator();
            var hubClients_ClientProxies = GetHubClients_Client_Proxies(expr, true);
            var multiEndpointHubClients = GetMultiEndpointHubClients(hubClients_ClientProxies, router);

            Task t = func(multiEndpointHubClients).SendCoreAsync(Method, args, token);

            await t.AssertThrowAggregationException(EndpointCount);
        }

        public static readonly TheoryData<Expression<Func<IHubClients<IClientProxy>, IClientProxy>>, Expression<Func<IEndpointRouter, IEnumerable<ServiceEndpoint>>>> dataTestedWithSingleRouter = new TheoryData<Expression<Func<IHubClients<IClientProxy>, IClientProxy>>, Expression<Func<IEndpointRouter, IEnumerable<ServiceEndpoint>>>>
        {
            {c=>c.Group(groupNames[0]),router => router.GetEndpointsForGroup(groupNames[0], _endpoints) },
            {c=>c.GroupExcept(groupNames[0],connectionIds),router => router.GetEndpointsForGroup(groupNames[0], _endpoints) },
            {c=>c.Client(connectionIds[0]),router => router.GetEndpointsForConnection(connectionIds[0], _endpoints) },
            {c=>c.User(userIds[0]),router=>router.GetEndpointsForUser(userIds[0],_endpoints) }
        };

        [Theory]
        [MemberData(nameof(dataTestedWithSingleRouter))]
        public async void WithSingleRouter_Normal_Theories(Expression<Func<IHubClients<IClientProxy>, IClientProxy>> testMethodexpr, Expression<Func<IEndpointRouter, IEnumerable<ServiceEndpoint>>> routerExpr)
        {
            ServiceEndpoint[] endpointsRouterTo = new ServiceEndpoint[] { _endpoints[0] };
            var router = GetMockRouter(mock =>
            {
                mock.Setup(routerExpr).Returns(endpointsRouterTo);
            });
            var hubClients_ClientProxies = GetHubClients_Client_Proxies(testMethodexpr);
            var multiEndpointHubClients = GetMultiEndpointHubClients(hubClients_ClientProxies, router);
            var func = testMethodexpr.Compile();
            var sendExpr = CreateSendExpr(testMethodexpr);

            await func(multiEndpointHubClients).SendCoreAsync(Method, args, token);

            hubClients_ClientProxies[0].IClientProxyMock.Verify(sendExpr, Times.Once);
            hubClients_ClientProxies[1].IClientProxyMock.Verify(sendExpr, Times.Never);
            hubClients_ClientProxies[2].IClientProxyMock.Verify(sendExpr, Times.Never);
        }

        public static readonly TheoryData<Expression<Func<IHubClients<IClientProxy>, IClientProxy>>, Expression<Func<IEndpointRouter, IEnumerable<ServiceEndpoint>>>> dataTestedWithMultiRouter = new TheoryData<Expression<Func<IHubClients<IClientProxy>, IClientProxy>>, Expression<Func<IEndpointRouter, IEnumerable<ServiceEndpoint>>>>
        {
            {c=>c.Groups(groupNames),router => router.GetEndpointsForGroup(It.IsAny<string>(), _endpoints)},
            {c=>c.Clients(connectionIds),router => router.GetEndpointsForConnection(It.IsAny<string>(), _endpoints) },
            {c=>c.Users(userIds),router=>router.GetEndpointsForUser(It.IsAny<string>(), _endpoints) }
        };

        [Theory]
        [MemberData(nameof(dataTestedWithMultiRouter))]
        public async void WithMultiRouter_Normal_Theories(Expression<Func<IHubClients<IClientProxy>, IClientProxy>> testMethodExpr, Expression<Func<IEndpointRouter, IEnumerable<ServiceEndpoint>>> routerExpr)
        {
            var router = GetMockRouter(mock =>
            {
                mock.Setup(routerExpr)
                    .Returns(new ServiceEndpoint[] { _endpoints[0], _endpoints[1] });
            });
            var hubClients_ClientProxies = GetHubClients_Client_Proxies(testMethodExpr);
            var multiEndpointHubClients = GetMultiEndpointHubClients(hubClients_ClientProxies, router);
            var sendExpr = CreateSendExpr(testMethodExpr);
            var func = testMethodExpr.Compile();

            await func(multiEndpointHubClients).SendCoreAsync(Method, args, token);

            hubClients_ClientProxies[0].IClientProxyMock.Verify(sendExpr, Times.Once);
            hubClients_ClientProxies[1].IClientProxyMock.Verify(sendExpr, Times.Once);
            hubClients_ClientProxies[2].IClientProxyMock.Verify(sendExpr, Times.Never);
        }

        private List<(Mock<IHubClients> IHubClientsMock, Mock<IHubClients<IClientProxy>> IClientProxyMock)> GetHubClients_Client_Proxies(Expression<Func<IHubClients<IClientProxy>, IClientProxy>> expr, bool throwException = false)
        {
            var sendExpr = CreateSendExpr(expr);
            return Enumerable.Range(0, EndpointCount)
                .Select(_ =>
                {
                    var IHubClientsMock = new Mock<IHubClients>();
                    var IClientProxyMock = IHubClientsMock.As<IHubClients<IClientProxy>>();
                    IClientProxyMock.Setup(expr);
                    var setup = IClientProxyMock.Setup(sendExpr);
                    if (throwException)
                    {
                        setup.ThrowsAsync(new Exception());
                    }

                    return (IHubClientsMock, IClientProxyMock);
                }).ToList();
        }

        private Expression<Func<IHubClients<IClientProxy>, Task>> CreateSendExpr(Expression<Func<IHubClients<IClientProxy>, IClientProxy>> expr)
        //return a lambda  (IHubClients<IClientProxy> c)=>expr.SendAsyncCore(Method,args,token);
        {
            var methodCallExpression = Expression.Call(expr.Body, expr.ReturnType.GetMethod("SendCoreAsync"), Expression.Constant(Method), Expression.Constant(args), Expression.Constant(token));
            return Expression.Lambda<Func<IHubClients<IClientProxy>, Task>>(methodCallExpression, Expression.Parameter(typeof(IHubClients<IClientProxy>)));
        }

        private MultiEndpointHubClients GetMultiEndpointHubClients(List<(Mock<IHubClients> IHubClientsMock, Mock<IHubClients<IClientProxy>> IClientProxyMock)> pair, IEndpointRouter router)
        {
            var table = _endpoints.Zip(pair).ToDictionary(pair => pair.First, pair => pair.Second.IHubClientsMock.Object);
            return new MultiEndpointHubClients(router, table);
        }

        private IEndpointRouter GetMockRouter(Action<Mock<IEndpointRouter>> action = null)
        {
            var routerMock = new Mock<IEndpointRouter>();
            action?.Invoke(routerMock);
            return routerMock.Object;
        }
    }
}