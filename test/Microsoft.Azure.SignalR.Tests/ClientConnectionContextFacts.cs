// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Protocol;
using Xunit;

using ServiceProtocol = Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.Tests;

#nullable enable

public class ClientConnectionContextFacts
{
    public static byte[] EmptyHandshakeResponse { get; } = HandshakeProtocol.GetSuccessfulHandshake(new JsonHubProtocol()).ToArray();

    [Fact]
    public void SetUserIdFeatureTest()
    {
        var claims = new Claim[] { new(Constants.ClaimType.UserId, "testUser") };
        var connection = new ClientConnectionContext(new("connectionId", claims));
        var feature = connection.Features.Get<ServiceUserIdFeature>();
        Assert.NotNull(feature);
        Assert.Equal("testUser", feature.UserId);
    }

    [Fact]
    public void DoNotSetUserIdFeatureWithoutUserIdClaimTest()
    {
        var connection = new ClientConnectionContext(new("connectionId", Array.Empty<Claim>()));
        var feature = connection.Features.Get<ServiceUserIdFeature>();
        Assert.Null(feature);
    }

    [Fact]
    public async void TestForwardCloseMessage()
    {
        using var serviceConnection = new TestServiceConnection();

        var pipeOptions = new PipeOptions();
        var pair = DuplexPipe.CreateConnectionPair(pipeOptions, pipeOptions);

        var connectionId = "testConnectionId";
        var connection = new ClientConnectionContext(new(connectionId, Array.Empty<Claim>()))
        {
            Application = pair.Application,
            ServiceConnection = serviceConnection
        };

        var protocol = new JsonHubProtocol();
        var outgoingTask = connection.ProcessOutgoingMessagesAsync(protocol);

        // write handshake response
        var response = HandshakeResponseMessage.Empty;
        HandshakeProtocol.WriteResponseMessage(response, pair.Transport.Output);

        var closeMessage = new CloseMessage("foo");
        protocol.WriteMessage(closeMessage, pair.Transport.Output);
        await pair.Transport.Output.FlushAsync();

        // complete Tranport layer to stop outgoing messages async.
        pair.Transport.Output.Complete();
        await outgoingTask.OrTimeout();
        await serviceConnection.CompleteAsync();

        Assert.Equal(2, serviceConnection.Messages.Count);

        // parse close message
        var message = Assert.IsType<ServiceProtocol.ConnectionDataMessage>(serviceConnection.Messages[1]);
        Assert.Equal(ServiceProtocol.DataMessageType.Close, message.Type);
        Assert.Equal(connectionId, message.ConnectionId);

        Assert.Equal(protocol.GetMessageBytes(closeMessage).ToArray(), message.Payload.ToArray());
    }

    [Fact]
    public async void TestForwardInvocationMessage()
    {
        using var serviceConnection = new TestServiceConnection();

        var pipeOptions = new PipeOptions();
        var pair = DuplexPipe.CreateConnectionPair(pipeOptions, pipeOptions);

        var connectionId = "testConnectionId";
        var connection = new ClientConnectionContext(new(connectionId, Array.Empty<Claim>()))
        {
            Application = pair.Application,
            ServiceConnection = serviceConnection
        };

        var protocol = new JsonHubProtocol();
        var outgoingTask = connection.ProcessOutgoingMessagesAsync(protocol);

        // write handshake response
        var response = HandshakeResponseMessage.Empty;
        HandshakeProtocol.WriteResponseMessage(response, pair.Transport.Output);

        var invocationMessage = new InvocationMessage("invocationId", "foo", new string[] { "1", "2" });
        protocol.WriteMessage(invocationMessage, pair.Transport.Output);
        await pair.Transport.Output.FlushAsync();

        // complete Tranport layer to stop outgoing messages async.
        pair.Transport.Output.Complete();
        await outgoingTask.OrTimeout();
        await serviceConnection.CompleteAsync();

        Assert.Equal(2, serviceConnection.Messages.Count);

        // parse invocation message
        var message = Assert.IsType<ServiceProtocol.ConnectionDataMessage>(serviceConnection.Messages[1]);
        Assert.Equal(ServiceProtocol.DataMessageType.Invocation, message.Type);
        Assert.Equal(connectionId, message.ConnectionId);

        Assert.Equal(protocol.GetMessageBytes(invocationMessage).ToArray(), message.Payload.ToArray());
    }

    [Fact]
    public async void TestForwardHandshakeResponse()
    {
        using var serviceConnection = new TestServiceConnection();

        var pipeOptions = new PipeOptions();
        var pair = DuplexPipe.CreateConnectionPair(pipeOptions, pipeOptions);

        var connectionId = "testConnectionId";
        var connection = new ClientConnectionContext(new(connectionId, Array.Empty<Claim>()))
        {
            Application = pair.Application,
            ServiceConnection = serviceConnection
        };

        var protocol = new JsonHubProtocol();
        var outgoingTask = connection.ProcessOutgoingMessagesAsync(protocol);

        // write handshake response
        var response = HandshakeResponseMessage.Empty;
        HandshakeProtocol.WriteResponseMessage(response, pair.Transport.Output);
        await pair.Transport.Output.FlushAsync();

        // complete Tranport layer to stop outgoing messages async.
        pair.Transport.Output.Complete();
        await outgoingTask.OrTimeout();
        await serviceConnection.CompleteAsync();

        Assert.Single(serviceConnection.Messages);

        // parse handshake response
        var message = Assert.IsType<ServiceProtocol.ConnectionDataMessage>(serviceConnection.Messages[0]);
        Assert.Equal(ServiceProtocol.DataMessageType.Handshake, message.Type);
        Assert.Equal(connectionId, message.ConnectionId);

        Assert.Equal(EmptyHandshakeResponse, message.Payload.ToArray());
    }

    [Fact]
    public async void TestSkipHandshakeResponse()
    {
        using var serviceConnection = new TestServiceConnection();

        var pipeOptions = new PipeOptions();
        var pair = DuplexPipe.CreateConnectionPair(pipeOptions, pipeOptions);

        var connectionId = "testConnectionId";
        var connection = new ClientConnectionContext(new(connectionId, Array.Empty<Claim>())
        {
            Headers =
            {
                { Constants.AsrsMigrateFrom, "from-server"}
            }
        })
        {
            Application = pair.Application,
            ServiceConnection = serviceConnection
        };

        var protocol = new JsonHubProtocol();
        var outgoingTask = connection.ProcessOutgoingMessagesAsync(protocol);

        // write handshake response
        var response = HandshakeResponseMessage.Empty;
        HandshakeProtocol.WriteResponseMessage(response, pair.Transport.Output);
        await pair.Transport.Output.FlushAsync();

        // complete Tranport layer to stop outgoing messages async.
        pair.Transport.Output.Complete();
        await outgoingTask.OrTimeout();
        await serviceConnection.CompleteAsync();

        Assert.Empty(serviceConnection.Messages);
    }

    private sealed class TestServiceConnection : IServiceConnection, IDisposable
    {
        private readonly Pipe _pipe = new();

        public List<ServiceProtocol.ServiceMessage> Messages { get; } = new();

        public string ConnectionId => throw new NotImplementedException();

        public string ServerId => throw new NotImplementedException();

        public ServiceConnectionStatus Status => throw new NotImplementedException();

        public Task ConnectionInitializedTask => throw new NotImplementedException();

        public Task ConnectionOfflineTask => throw new NotImplementedException();

        private readonly Task _lifetimeTask;

        private ServiceProtocol.ServiceProtocol ServiceProtocol { get; } = new ServiceProtocol.ServiceProtocol();

        public TestServiceConnection()
        {
            _lifetimeTask = StartAsync();
        }

        public event Action<StatusChange>? ConnectionStatusChanged;

        public Task<bool> SafeWriteAsync(ServiceProtocol.ServiceMessage serviceMessage)
        {
            throw new NotImplementedException();
        }

        public Task StopAsync()
        {
            throw new NotImplementedException();
        }

        public async Task StartAsync(string? target = null)
        {
            while (true)
            {
                var r = await _pipe.Reader.ReadAsync();
                var buffer = r.Buffer;

                try
                {
                    if (r.IsCanceled)
                    {
                        break;
                    }

                    if (!buffer.IsEmpty)
                    {
                        while (ServiceProtocol.TryParseMessage(ref buffer, out var message))
                        {
                            Messages.Add(message);
                        }
                    }

                    if (r.IsCompleted)
                    {
                        break;
                    }
                }
                catch (TimeoutException)
                {
                    // do nothing
                }
                finally
                {
                    _pipe.Reader.AdvanceTo(buffer.Start, buffer.End);
                }
            }
        }

        public async Task WriteAsync(ServiceProtocol.ServiceMessage serviceMessage)
        {
            ServiceProtocol.WriteMessage(serviceMessage, _pipe.Writer);
            await _pipe.Writer.FlushAsync();
        }

        public async Task CompleteAsync()
        {
            _pipe.Writer.Complete();
            await _lifetimeTask;
        }

        public void Dispose()
        {
        }
    }
}
