// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Azure.SignalR.Tests.TestHubs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Tests;

public class RunSignalRTests : VerifiableLoggedTest
{
    private readonly ITestOutputHelper _output;

    public RunSignalRTests(ITestOutputHelper output) : base(output)
    {
        _output = output;
    }

    [Fact]
    public async Task TestRunSignalRWithSimpleHub()
    {
        var provider = new LogSinkProvider();
        var cdf = new CaptureDataConnectionFactory();
        var startup = new TestStartup<SimpleHub>(services =>
        {
            services.AddSingleton<IConnectionFactory>(cdf);
        });
        var builder = WebHost.CreateDefaultBuilder()
            .ConfigureLogging(logging => logging.AddXunit(_output))
            .ConfigureLogging(logging => logging.AddProvider(provider))
            .ConfigureLogging(logging => logging.AddFilter("Microsoft.Azure.SignalR", LogLevel.Debug))
            .ConfigureLogging(logging => logging.AddFilter("Microsoft.AspNetCore.DataProtection", LogLevel.None))
            .UseStartup(c => startup);

        using (var server = new TestServer(builder))
        {
            var sc = await cdf.FirstConnectionTask.OrTimeout();
            await sc.OpenClientConnectionAsync("conn1").OrTimeout();

            var ccm = server.Services.GetService<IClientConnectionManager>();

            await Utils.PollWait(() => ccm.TryGetClientConnection("conn1", out var connection));
            await sc.WriteServiceFinAck();
        }

        var logs = provider.GetLogs();
        Assert.Single(logs.Where(s => s.Write.EventId.Name == "StoppingServer"));
        Assert.Single(logs.Where(s => s.Write.EventId.Name == "CloseClientConnections"));

        var connectionDisconnectLog = logs.FirstOrDefault(s => s.Write.LoggerName == typeof(SimpleHub).FullName);
        Assert.NotNull(connectionDisconnectLog);
        Assert.Equal("conn1 disconnected: .", connectionDisconnectLog.Write.Message);

        Assert.Empty(logs.Where(s => s.Write.LogLevel == LogLevel.Warning));
    }

    [Fact]
    public async Task TestRunSignalRWithSimpleHubAndMultipleConnections()
    {
        var provider = new LogSinkProvider();
        var cdf = new CaptureDataConnectionFactory();
        var startup = new TestStartup<SimpleHub>(services =>
        {
            services.AddSingleton<IConnectionFactory>(cdf);
        });
        var builder = WebHost.CreateDefaultBuilder()
            .ConfigureLogging(logging => logging.AddXunit(_output))
            .ConfigureLogging(logging => logging.AddProvider(provider))
            .ConfigureLogging(logging => logging.AddFilter("Microsoft.AspNetCore.DataProtection", LogLevel.None))
            .UseStartup(c => startup);
        const int count = 1111;

        using (var server = new TestServer(builder))
        {
            var sc = await cdf.FirstConnectionTask.OrTimeout();
            for (var i = 0; i < count; i++)
            {
                await sc.OpenClientConnectionAsync("conn" + i).OrTimeout();
            }

            var ccm = server.Services.GetService<IClientConnectionManager>();

            await Utils.PollWait(() => ccm.Count == count).OrTimeout();
            await sc.WriteServiceFinAck();
        }

        var logs = provider.GetLogs();
        Assert.Single(logs.Where(s => s.Write.EventId.Name == "StoppingServer"));
        Assert.Single(logs.Where(s => s.Write.EventId.Name == "CloseClientConnections"));
        Assert.Equal(0, logs.Count(s => s.Write.EventId.Name == "DetectedLongRunningApplicationTask"));

        Assert.Equal(count, logs.Count(s => s.Write.LoggerName == typeof(SimpleHub).FullName));
        Assert.Empty(logs.Where(s => s.Write.LogLevel == LogLevel.Warning));
    }

    [Fact]
    public async Task TestRunSignalRWithConnectedSendingMessages()
    {
        var provider = new LogSinkProvider();
        var cdf = new CaptureDataConnectionFactory();
        var startup = new TestStartup<ConnectedHub>(services =>
        {
            services.AddSingleton<IConnectionFactory>(cdf);
        });
        var builder = WebHost.CreateDefaultBuilder()
            .ConfigureLogging(logging => logging.AddXunit(_output))
            .ConfigureLogging(logging => logging.AddProvider(provider))
            .ConfigureLogging(logging => logging.AddFilter("Microsoft.AspNetCore.DataProtection", LogLevel.None))
            .UseStartup(c => startup);

        using (var server = new TestServer(builder))
        {
            var sc = await cdf.FirstConnectionTask.OrTimeout();
            await sc.OpenClientConnectionAsync("conn1").OrTimeout();

            var ccm = server.Services.GetService<IClientConnectionManager>();

            await Utils.PollWait(() => ccm.TryGetClientConnection("conn1", out var connection));

            await sc.WriteServiceFinAck();
        }

        var logs = provider.GetLogs();
        Assert.Single(logs.Where(s => s.Write.EventId.Name == "StoppingServer"));
        Assert.Single(logs.Where(s => s.Write.EventId.Name == "CloseClientConnections"));
        Assert.Single(logs.Where(s => s.Write.EventId.Name == "DetectedLongRunningApplicationTask"));

        var connectionDisconnectLog = logs.FirstOrDefault(s => s.Write.LoggerName == typeof(ConnectedHub).FullName);
        Assert.NotNull(connectionDisconnectLog);
        Assert.Equal("conn1 disconnected: .", connectionDisconnectLog.Write.Message);
        Assert.Empty(logs.Where(s => s.Write.LogLevel == LogLevel.Warning && s.Write.EventId.Name != "DetectedLongRunningApplicationTask").Select(s => s.Write.EventId.Name));
    }

    private sealed class TestStartup<THub> : IStartup
        where THub : Hub
    {
        private readonly Action<IServiceCollection> _configureServices;

        public TestStartup(Action<IServiceCollection> configureServices)
        {
            _configureServices = configureServices;
        }
        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();
            app.UseEndpoints(configure =>
            {
                configure.MapHub<THub>($"/{nameof(THub)}");
            });
            app.UseMvc();
        }

        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            services.AddMvc(option => option.EnableEndpointRouting = false);
            services
                .AddSignalR(options =>
                {
                    options.EnableDetailedErrors = true;
                })
                .AddAzureSignalR(o =>
                {
                    o.ConnectionString = FakeEndpointUtils.GetFakeConnectionString(1).First();
                    o.InitialHubServerConnectionCount = 1;
                    o.MaxHubServerConnectionCount = 1;
                });
            _configureServices.Invoke(services);
            return services.BuildServiceProvider();
        }
    }

    private sealed class ControlledServiceConnectionContext : ConnectionContext
    {
        private static readonly ServiceProtocol _serviceProtocol = new ServiceProtocol();
        private static readonly JsonHubProtocol _signalRPro = new JsonHubProtocol();
        public ControlledServiceConnectionContext()
        {
            var pipe = DuplexPipe.CreateConnectionPair(new PipeOptions(pauseWriterThreshold: 0), new PipeOptions(pauseWriterThreshold: 0));
            Transport = pipe.Transport;
            Application = pipe.Application;
            // Write handshake response
            _ = WriteHandshakeResponseAsync(Application.Output);
        }

        private async Task WriteHandshakeResponseAsync(PipeWriter output)
        {
            _serviceProtocol.WriteMessage(new Protocol.HandshakeResponseMessage(), output);
            var sendHandshakeResult = await output.FlushAsync();
        }

        public override IDuplexPipe Transport { get; set; }

        public IDuplexPipe Application { get; set; }
        public override string ConnectionId { get; set; }
        public override IFeatureCollection Features { get; }
        public override IDictionary<object, object> Items { get; set; }

        public async Task OpenClientConnectionAsync(string connectionId)
        {
            var openClientConnMsg = new OpenConnectionMessage(connectionId, new System.Security.Claims.Claim[] { }) { Protocol = "json" };
            _serviceProtocol.WriteMessage(openClientConnMsg, Application.Output);
            await Application.Output.FlushAsync();

            var clientHandshakeRequest = new AspNetCore.SignalR.Protocol.HandshakeRequestMessage("json", 1);
            var clientHandshake = new ConnectionDataMessage(connectionId, GetMessageBytes(clientHandshakeRequest));
            _serviceProtocol.WriteMessage(clientHandshake, Application.Output);
            await Application.Output.FlushAsync();
        }

        public ValueTask<FlushResult> WriteServiceFinAck()
        {
            _serviceProtocol.WriteMessage(RuntimeServicePingMessage.GetFinAckPingMessage(), Application.Output);
            return Application.Output.FlushAsync();
        }

        public static ReadOnlyMemory<byte> GetMessageBytes(Microsoft.AspNetCore.SignalR.Protocol.HandshakeRequestMessage message)
        {
            var writer = MemoryBufferWriter.Get();
            try
            {
                HandshakeProtocol.WriteRequestMessage(message, writer);
                return writer.ToArray();
            }
            finally
            {
                MemoryBufferWriter.Return(writer);
            }
        }
    }

    private sealed class CaptureDataConnectionFactory : IConnectionFactory
    {
        private TaskCompletionSource<ControlledServiceConnectionContext> _taskCompletionSource = new TaskCompletionSource<ControlledServiceConnectionContext>();
        public Task<ControlledServiceConnectionContext> FirstConnectionTask => _taskCompletionSource.Task;
        public Task DisposeAsync(ConnectionContext connection)
        {
            return Task.CompletedTask;
        }

        Task<ConnectionContext> IConnectionFactory.ConnectAsync(HubServiceEndpoint endpoint, TransferFormat transferFormat, string connectionId, string target, CancellationToken cancellationToken, IDictionary<string, string> headers)
        {
            var connection = new ControlledServiceConnectionContext();
            _taskCompletionSource.TrySetResult(connection);
            return Task.FromResult<ConnectionContext>(connection);
        }
    }
}
