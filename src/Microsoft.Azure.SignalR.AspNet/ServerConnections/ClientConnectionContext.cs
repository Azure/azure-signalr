// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.SignalR.AspNet;

internal class ClientConnectionContext : IClientConnection
{
    private readonly CancellationTokenSource _source = new CancellationTokenSource();

    public string ConnectionId { get; }

    public string InstanceId { get; }

    public IServiceConnection ServiceConnection { get; set; }

    public Task ApplicationTask { get; set; }

    public CancellationToken CancellationToken => _source.Token;

    public ChannelReader<ServiceMessage> Input { get; }

    public ChannelWriter<ServiceMessage> Output { get; }

    public IServiceTransport Transport { get; set; }

    public string HubProtocol { get; }

    public ClientConnectionContext(OpenConnectionMessage serviceMessage)
    {
        InstanceId = GetInstanceId(serviceMessage.Headers);
        ConnectionId = serviceMessage.ConnectionId;
        HubProtocol = serviceMessage.Protocol;

        var channel = Channel.CreateUnbounded<ServiceMessage>();
        Input = channel.Reader;
        Output = channel.Writer;
    }

    public void CancelPendingRead()
    {
        _source.Cancel();
    }

    private string GetInstanceId(IDictionary<string, StringValues> header)
    {
        return header.TryGetValue(Constants.AsrsInstanceId, out var instanceId) ? (string)instanceId : string.Empty;
    }
}
