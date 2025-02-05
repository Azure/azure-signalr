// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.E2ETests.Common;

public interface ITestHubConnection
{
    public string ConnectionId { get; }

    public string User { get; }

    public int MessageCount { get; }

    public void Listen(params string[] methods);

    public Task StartAsync();

    public Task StopAsync();

    public Task SendAsync(string method, params string[] messages);

    public void ResetInvoke(string method);

    public Task ExpectInvokeAsync(string method, params string[] message);

    public void ResetMessageCount();
}
