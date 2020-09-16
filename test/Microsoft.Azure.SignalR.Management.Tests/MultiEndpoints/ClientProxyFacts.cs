// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Xunit;
namespace Microsoft.Azure.SignalR.Management.Tests.MultiEndpoints
{
    public class ClientProxyFacts
    {
        [Fact]
        public async Task SendCoreAsync_Normal_Fact()
        {
            int proxyNum = 3;
            string methodName = "methodName";
            object[] agrs = new object[0];
            var token = new CancellationTokenSource().Token;
            var mocks = Enumerable.Range(0, proxyNum)
                .Select(_ =>
                {
                    var mock = new Mock<IClientProxy>();
                    mock.Setup(proxy => proxy.SendCoreAsync(methodName, agrs, token))
                        .Returns(Task.CompletedTask);
                    return mock;
                })
                .ToList();
            var mockProxies = mocks.Select(mock => mock.Object);
            var MeClientProxy = new MultiEndpointClientProxy(mockProxies);

            await MeClientProxy.SendCoreAsync(methodName, agrs, token);

            foreach (var mock in mocks)
            {
                mock.Verify(proxy => proxy.SendCoreAsync(methodName, agrs, token), Times.Once);
            }
        }

        [Fact]
        public async Task SendCoreAsync_Throw_Fact()
        {
            int proxyNum = 3;
            var exceptions = Enumerable.Range(0, proxyNum)
                .Select(_ => new Exception());
            //each single ClientProxy throws an exception with message $"{id}"
            var mocks = exceptions
                .Select(exception =>
                {
                    var mock = new Mock<IClientProxy>();
                    mock.Setup(proxy => proxy.SendCoreAsync(default, default, default))
                        .ThrowsAsync(exception);
                    return mock;
                });
            var mockProxies = mocks.Select(mock => mock.Object);
            var MeClientProxy = new MultiEndpointClientProxy(mockProxies);
            var aggreExpHelper = new AggreExcpVerificationHelper();

            Task t = MeClientProxy.SendCoreAsync(default, default, default);

            await aggreExpHelper.AssertIsAggreExp(proxyNum, t);
        }
    }
}