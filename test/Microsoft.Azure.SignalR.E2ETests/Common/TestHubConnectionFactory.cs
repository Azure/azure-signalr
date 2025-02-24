// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.SignalR.Client;

using Xunit;

namespace Microsoft.Azure.SignalR.E2ETests.Common;

public class TestHubConnectionFactory
{
    public bool EnableStatefulReconnect { get; set; }

    public ITestHubConnection NewConnection(string host, string? hub = null, string userId = "foo")
    {
        return new TestHubConnection(host, hub)
        {
            User = userId,
            EnableStatefulReconnect = EnableStatefulReconnect
        };
    }

    public ITestHubConnectionGroup NewConnectionGroup(string host, int count, string? hub = null, string userId = "foo")
    {
        return new TestHubConnectionGroup(host, count, hub)
        {
            User = userId,
            EnableStatefulReconnect = EnableStatefulReconnect
        };
    }

    private sealed class TestHubConnection(string host, string? hub = null) : ITestHubConnection
    {
        private readonly ConcurrentDictionary<string, TaskCompletionSource<string[]>> _expectedInvokes = new();

        private HubConnection? _hubConnection;

        private volatile int _messageCount;

        public string User { get; init; } = "foo";

        public int MessageCount => _messageCount;

        public bool EnableStatefulReconnect { get; init; }

        public string ConnectionId => _hubConnection?.ConnectionId ?? throw NotReady;

        private static Exception NotReady { get; } = new InvalidOperationException("HubConnection is not in connected state.");

        public Task StartAsync()
        {
            BuildConnectionIfNull();
            return _hubConnection.StartAsync();
        }

        public Task StopAsync() => _hubConnection?.StopAsync() ?? Task.CompletedTask;

        public Task SendAsync(string method, params string[] messages)
        {
            if (_hubConnection == null || _hubConnection.State != HubConnectionState.Connected)
            {
                throw NotReady;
            }
            return _hubConnection.SendCoreAsync(method, messages);
        }

        public void Listen(params string[] methods)
        {
            BuildConnectionIfNull();
            foreach (var method in methods)
            {
                _expectedInvokes.TryAdd(method, new TaskCompletionSource<string[]>());
                _hubConnection.On(method, (Action<string>)(message => Invoke(method, message)));
            }
        }

        public void ResetInvoke(string method)
        {
            _expectedInvokes.AddOrUpdate(method,
                                         new TaskCompletionSource<string[]>(),
                                         (method, ov) => ov.Task.IsCompleted ? new TaskCompletionSource<string[]>() : ov);
        }

        public async Task ExpectInvokeAsync(string method, params string[] messages)
        {
            Assert.Equal(messages, await _expectedInvokes[method].Task);
        }

        public void ResetMessageCount()
        {
            _messageCount = 0;
        }

        private void Invoke(string method, params string[] messages)
        {
            Interlocked.Increment(ref _messageCount);
            if (_expectedInvokes.TryGetValue(method, out var source))
            {
                source.TrySetResult(messages);
            }
        }

        [MemberNotNull(nameof(_hubConnection))]
        private void BuildConnectionIfNull()
        {
            if (_hubConnection == null)
            {
                hub ??= nameof(TestHub);
                var url = $"{host}/{hub}?user={User}";
                var builder = new HubConnectionBuilder().WithUrl(url);

                if (EnableStatefulReconnect)
                {
                    builder = builder.WithStatefulReconnect();
                }
                _hubConnection = builder.Build();
            }
        }
    }

    private sealed class TestHubConnectionGroup(string host, int count, string? hub = null) : ITestHubConnectionGroup
    {
        private List<ITestHubConnection>? _connections;

        public bool EnableStatefulReconnect { get; init; }

        public IEnumerable<ITestHubConnection> Connections
        {
            get
            {
                _connections ??= (from i in Enumerable.Range(0, count)
                                  select new TestHubConnection(host, hub)
                                  {
                                      User = User,
                                      EnableStatefulReconnect = EnableStatefulReconnect,
                                  } as ITestHubConnection).ToList();
                return _connections;
            }
        }

        public string User { get; init; } = string.Empty;

        public int MessageCount => Connections.Select(x => x.MessageCount).Sum();

        public string ConnectionId => throw new NotImplementedException("Connection group does not have ConnectionId.");

        public void Listen(params string[] methods)
        {
            foreach (var connection in Connections)
            {
                connection.Listen(methods);
            }
        }

        public async Task StartAsync() => await Task.WhenAll(Connections.Select(x => x.StartAsync()));

        public IEnumerator<ITestHubConnection> GetEnumerator() => Connections.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public async Task StopAsync() => await Task.WhenAll(Connections.Select(x => x.StopAsync()));

        public void ResetInvoke(string method)
        {
            foreach (var connection in Connections)
            {
                connection.ResetInvoke(method);
            }
        }

        public Task ExpectInvokeAsync(string method, params string[] messages)
        {
            return Task.WhenAll(Connections.Select(x => x.ExpectInvokeAsync(method, messages)));
        }

        public Task SendAsync(string method, params string[] messages) => Task.WhenAll(Connections.Select(x => x.SendAsync(method, messages)));

        public void ResetMessageCount()
        {
            foreach (var connection in Connections)
            {
                connection.ResetMessageCount();
            }
        }
    }
}
