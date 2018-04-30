using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Azure.SignalR.Tests
{
    internal class ServiceConnectionProxy
    {
        private static readonly IServiceProtocol ServiceProtocol = new ServiceProtocol();
        private static readonly IInvocationBinder Binder = new InvocationBinder();
        private static readonly HubMessage Message = new InvocationMessage("target", new object[] {"argument"});

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private readonly IHubProtocol _hubProtocol;

        public ConcurrentDictionary<string, int> ConnectionMessageCounter { get; } =
            new ConcurrentDictionary<string, int>(StringComparer.Ordinal);

        public IConnectionFactory ConnectionFactory { get; }

        public IClientConnectionManager ClientConnectionManager { get; }

        public TestConnection ConnectionContext { get; }

        public ServiceConnection ServiceConnection { get; }

        public ServiceConnectionProxy(string hubProtocolName = "json")
        {
            ConnectionContext = new TestConnection();
            ConnectionFactory = new TestConnectionFactory(ConnectionContext);
            ClientConnectionManager = new ClientConnectionManager();

            _hubProtocol = hubProtocolName.Equals("json", StringComparison.OrdinalIgnoreCase)
                ? new JsonHubProtocol()
                : (IHubProtocol) new MessagePackHubProtocol();

            ServiceConnection = new ServiceConnection(
                ServiceProtocol,
                ClientConnectionManager,
                ConnectionFactory,
                NullLoggerFactory.Instance,
                MessageCounterConnectionDelegate,
                Guid.NewGuid().ToString("N"));
        }

        public void Start()
        {
            _ = ServiceConnection.StartAsync(_cts.Token);
        }

        public void Stop()
        {
            _cts.Cancel();
        }

        public ReadOnlyMemory<byte> GetHubMessageBytes()
        {
            return _hubProtocol.GetMessageBytes(Message);
        }

        private async Task MessageCounterConnectionDelegate(ConnectionContext connection)
        {
            var messageCount = 0;
            ConnectionMessageCounter.TryAdd(connection.ConnectionId, messageCount);
            try
            {
                while (true)
                {
                    var result = await connection.Transport.Input.ReadAsync(_cts.Token);

                    var buffer = result.Buffer;
                    var consumed = buffer.Start;
                    var examined = buffer.End;

                    try
                    {
                        if (!buffer.IsEmpty)
                        {
                            if (_hubProtocol.TryParseMessage(ref buffer, Binder, out var message))
                            {
                                consumed = buffer.Start;
                                examined = consumed;

                                ConnectionMessageCounter.TryUpdate(connection.ConnectionId, messageCount + 1,
                                    messageCount);
                                messageCount++;
                            }
                        }

                        if (result.IsCompleted)
                        {
                            break;
                        }
                    }
                    finally
                    {
                        connection.Transport.Input.AdvanceTo(consumed, examined);
                    }
                }
            }
            catch
            {
                // Ignore
            }
        }

        private class InvocationBinder : IInvocationBinder
        {
            public Type GetReturnType(string invocationId)
            {
                return typeof(object);
            }

            public IReadOnlyList<Type> GetParameterTypes(string methodName)
            {
                return new[]
                {
                    typeof(string)
                };
            }
        }
    }
}
