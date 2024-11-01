// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure;
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;
using static System.Net.Mime.MediaTypeNames;

namespace Microsoft.Azure.SignalR.IntegrationTests
{
    public class SimpleHub : Hub
    {
        public override Task OnConnectedAsync()
        {
            return Clients.All.SendAsync("hello");
        }
    }


    public class ControlledServiceConnectionContext : ConnectionContext
    {
        private static readonly ServiceProtocol _serviceProtocol = new ServiceProtocol();
        private static readonly JsonHubProtocol _signalRPro = new JsonHubProtocol();
        public ControlledServiceConnectionContext()
        {
            var pipe = DuplexPipe.CreateConnectionPair(new PipeOptions(), new PipeOptions());
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

    public class CaptureDataConnectionFactory : IConnectionFactory
    {
        private TaskCompletionSource<ControlledServiceConnectionContext> _taskCompletionSource = new TaskCompletionSource<ControlledServiceConnectionContext>();
        public Task<ControlledServiceConnectionContext> FirstConnectionTask => _taskCompletionSource.Task;
        public List<ControlledServiceConnectionContext> ServiceConnections { get; } = new();
        public Task DisposeAsync(ConnectionContext connection)
        {
            throw new NotImplementedException();
        }

        Task<ConnectionContext> IConnectionFactory.ConnectAsync(HubServiceEndpoint endpoint, TransferFormat transferFormat, string connectionId, string target, CancellationToken cancellationToken, IDictionary<string, string> headers)
        {
            var connection = new ControlledServiceConnectionContext();
            ServiceConnections.Add(connection);
            _taskCompletionSource.TrySetResult(connection);
            return Task.FromResult<ConnectionContext>(connection);
        }
    }

    public class StartupTests : VerifiableLoggedTest
    {
        private readonly ITestOutputHelper _output;

        public StartupTests(ITestOutputHelper output) : base(output)
        {
            _output = output;
        }

        [Fact]
        public async Task TestRunSignalR()
        {
            var cdf = new CaptureDataConnectionFactory();
            var builder = WebHost.CreateDefaultBuilder()
                .ConfigureServices((IServiceCollection services) =>
                {
                    services.AddSingleton<IConnectionFactory>(cdf);
                })
                .ConfigureLogging(logging => logging.AddXunit(_output))
                .ConfigureLogging(logging => logging.AddFilter("Microsoft.Azure.SignalR", LogLevel.Debug))
                .UseStartup<TestStartup<SimpleHub>>();

            using var server = new TestServer(builder);
            var sc = await cdf.FirstConnectionTask;
            await sc.OpenClientConnectionAsync("conn1");

            var ccm = server.Services.GetService<IClientConnectionManager>();

            await PollWait(() => ccm.TryGetClientConnection("conn1", out var connection));
            
        }

        public static async Task PollWait(Func<bool> pollFunc, int count = 10)
        {
            bool result = false;
            int times = 0;
            while (!result)
            {
                times++;
                result = pollFunc();
                if (result) return;
                else if (times > count) break;
                await Task.Delay(1000);
            }

            Assert.Fail("Poll wait failed");
        }
    }
    internal class TestStartup<THub> : IStartup
        where THub : Hub
    {
        public const string ApplicationName = "AppName";

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
            services.AddSignalR(options =>
            {
                options.EnableDetailedErrors = true;
            })
                .AddAzureSignalR(o =>
                {
                    o.ConnectionString = FakeEndpointUtils.GetFakeConnectionString(1).First();
                    o.InitialHubServerConnectionCount = 1;
                    o.MaxHubServerConnectionCount = 1;
                });

            return services.BuildServiceProvider();
        }
    }
}
