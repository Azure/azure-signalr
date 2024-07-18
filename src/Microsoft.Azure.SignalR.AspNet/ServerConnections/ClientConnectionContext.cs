// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.AspNet;

internal class ClientConnectionContext
{
    private readonly CancellationTokenSource _source = new CancellationTokenSource();

    private readonly IServiceConnection _serviceConnection;

    public Task ApplicationTask { get; set; }

    public CancellationToken CancellationToken => _source.Token;

    public string ConnectionId { get; }

    public ChannelReader<ServiceMessage> Input { get; }

    public string InstanceId { get; }

    public ChannelWriter<ServiceMessage> Output { get; }

    public IServiceTransport Transport { get; set; }

    public ClientConnectionContext(IServiceConnection sc, string connectionId, string instanceId = null)
    {
        _serviceConnection = sc;

        ConnectionId = connectionId;
        InstanceId = instanceId;
        var channel = Channel.CreateUnbounded<ServiceMessage>();
        Input = channel.Reader;
        Output = channel.Writer;
    }

    public async Task WriteMessageAsync(ConnectionDataMessage message)
    {
        await _serviceConnection.WriteAsync(message);
    }

    public void CancelPendingRead()
    {
        _source.Cancel();
    }
}
