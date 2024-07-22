// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.AspNet;

internal class ClientConnectionContext : IClientConnection
{
    private readonly CancellationTokenSource _source = new CancellationTokenSource();

    public string ConnectionId { get; }

    public string InstanceId { get; }

    public IServiceConnection ServiceConnection { get; }

    public Task ApplicationTask { get; set; }

    public CancellationToken CancellationToken => _source.Token;

    public ChannelReader<ServiceMessage> Input { get; }

    public ChannelWriter<ServiceMessage> Output { get; }

    public IServiceTransport Transport { get; set; }

    public ClientConnectionContext(IServiceConnection sc, string connectionId, string instanceId = null)
    {
        ServiceConnection = sc;
        ConnectionId = connectionId;
        InstanceId = instanceId;
        var channel = Channel.CreateUnbounded<ServiceMessage>();
        Input = channel.Reader;
        Output = channel.Writer;
    }

    public void CancelPendingRead()
    {
        _source.Cancel();
    }
}
