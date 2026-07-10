// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Management
{
    internal sealed class ServiceHubContextImpl : ServiceHubContext
    {
        private readonly string _hubName;
        private readonly IHubContext<Hub> _hubContext;
        private readonly IServiceHubLifetimeManager _lifetimeManager;
        private readonly NegotiateProcessor _negotiateProcessor;
        private readonly IServiceEndpointManager _endpointManager;

        private bool _disposing;
        internal IServiceProvider ServiceProvider { get; }

        public override IHubClients Clients => _hubContext.Clients;

        public override GroupManager Groups { get; }

        public override UserGroupManager UserGroups { get; }

        public override StreamingManager Streaming { get; }

        public override ClientManager ClientManager { get; }

        public ServiceHubContextImpl(string hubName, IHubContext<Hub> hubContext, IServiceHubLifetimeManager lifetimeManager, IServiceProvider serviceProvider, NegotiateProcessor negotiateProcessor, IServiceEndpointManager endpointManager, ILogger<ServiceHubContext> logger)
        {
            _hubName = hubName;
            _hubContext = hubContext;
            Groups = new GroupManagerAdapter(lifetimeManager);
            UserGroups = new UserGroupsManagerAdapter(lifetimeManager);
            Streaming = new StreamingManagerAdapter(lifetimeManager, logger);
            ClientManager = new ClientManagerAdapter(lifetimeManager);
            ServiceProvider = serviceProvider;
            _negotiateProcessor = negotiateProcessor;
            _endpointManager = endpointManager;
            _lifetimeManager = lifetimeManager;
        }

        public override ValueTask<NegotiationResponse> NegotiateAsync(NegotiationOptions options, CancellationToken cancellationToken)
        {
            return new ValueTask<NegotiationResponse>(_negotiateProcessor.NegotiateAsync(_hubName, options, cancellationToken));
        }

        public override async Task<RefreshAuthResult> RefreshAuthAsync(string connectionToken, DateTimeOffset expireTime, IEnumerable<Claim> claims = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(connectionToken))
            {
                throw new ArgumentException("Argument cannot be null or empty.", nameof(connectionToken));
            }

            var result = await _lifetimeManager.RefreshAuthAsync(connectionToken, expireTime, claims, cancellationToken);
            ThrowOnNonSuccess(result.Status);

            if (result.Claims == null || result.Claims.Count == 0)
            {
                throw new AzureSignalRException("The service did not return the post-refresh claim set required to mint the refreshed access token.");
            }

            var accessTokenLifetime = Constants.Periods.DefaultAccessTokenLifetime;
            ServiceEndpoint owningEndpoint = result.OwningEndpoint;
            if (owningEndpoint == null)
            {
                var endpoints = _endpointManager.GetEndpoints(_hubName);
                if (endpoints.Count == 0)
                {
                    throw new AzureSignalRException("No service endpoint is available to mint the refreshed access token.");
                }
                owningEndpoint = endpoints[0];
            }
            var provider = _endpointManager.GetEndpointProvider(owningEndpoint);
            var accessToken = await provider.GenerateClientAccessTokenAsync(_hubName, result.Claims, accessTokenLifetime);
            var remainingSeconds = (expireTime - DateTimeOffset.UtcNow).TotalSeconds;
            var tokenLifetimeSeconds = (int)Math.Max(0, Math.Min(accessTokenLifetime.TotalSeconds, remainingSeconds));
            return new RefreshAuthResult(accessToken, tokenLifetimeSeconds);
        }

        public override async Task<ConnectionClaimsResult> GetConnectionClaimsAsync(string connectionToken, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(connectionToken))
            {
                throw new ArgumentException("Argument cannot be null or empty.", nameof(connectionToken));
            }

            var result = await _lifetimeManager.GetConnectionClaimsAsync(connectionToken, cancellationToken);
            ThrowOnNonSuccess(result.Status);
            return new ConnectionClaimsResult(result.Claims ?? Array.Empty<Claim>());
        }

        private static void ThrowOnNonSuccess(AckStatus status)
        {
            switch (status)
            {
                case AckStatus.Ok:
                    return;
                case AckStatus.Forbidden:
                    throw new AzureSignalRException("The refreshed token resolves to a different user (ConnectionUserMismatch).");
                case AckStatus.NotFound:
                    throw new AzureSignalRException("The target connection was not found, is disconnected, or uses an unsupported negotiate version.");
                case AckStatus.Timeout:
                    throw new AzureSignalRException("The operation timed out before the service acknowledged it.");
                default:
                    throw new AzureSignalRException("An unexpected error occurred while processing the request.");
            }
        }

        public override IEnumerable<ServiceEndpoint> GetServiceEndpoints() => _endpointManager.GetEndpoints(_hubName);

        public override async Task DisposeAsync()
        {
            // Check _disposed to avoid disposing twice.
            // When host is diposed, it will dispose all the disposable services including this class.
            if (!_disposing)
            {
                _disposing = true;
                using var host = ServiceProvider.GetRequiredService<IHost>();
                await host.StopAsync();
            }
        }

        public override ServiceHubContext WithEndpoints(IEnumerable<ServiceEndpoint> endpoints)
        {
#if NET6_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(endpoints);
#else
            if (endpoints is null)
            {
                throw new ArgumentNullException(nameof(endpoints));
            }
#endif

            var targetEndpoints = _endpointManager.GetEndpoints(_hubName).Intersect(endpoints, EqualityComparer<ServiceEndpoint>.Default).ToList();
            var container = new MessageWriterServiceContainerWrapper(targetEndpoints, ServiceProvider.GetRequiredService<ILoggerFactory>());
            var servicesFromServiceManager = ServiceProvider.GetRequiredService<IReadOnlyCollection<ServiceDescriptor>>();
            var services = new ServiceCollection()
                .Add(servicesFromServiceManager)
                //Allow chained call serviceHubContext.WithEndpoints(...).WithEndpoints(...)
                .AddSingleton(servicesFromServiceManager)
                //overwrite container
                .AddSingleton<IServiceConnectionContainer>(container)
                //add required service instances
                .AddSingleton(ServiceProvider.GetRequiredService<IOptions<ServiceManagerOptions>>())
                .AddSingleton(_endpointManager)
                .AddSingleton<IEndpointRouter>(new FixedEndpointRouter(targetEndpoints));

            return services.BuildServiceProvider().GetRequiredService<ServiceHubContext>();
        }

        private sealed class MessageWriterServiceContainerWrapper : MultiEndpointMessageWriter, IServiceConnectionContainer
        {
            public MessageWriterServiceContainerWrapper(IReadOnlyCollection<ServiceEndpoint> targetEndpoints, ILoggerFactory loggerFactory)
            : base(targetEndpoints, loggerFactory) { }

            public Task StartAsync() => Task.CompletedTask;

            public Task StopAsync() => Task.CompletedTask;

            public void Dispose()
            {
            }

            #region Not supported method or properties

            public ServiceConnectionStatus Status => throw new NotSupportedException();

            public string ServersTag => throw new NotSupportedException();

            public bool HasClients => throw new NotSupportedException();

            public Task OfflineAsync(GracefulShutdownMode mode, CancellationToken token) => throw new NotSupportedException();

            public Task StartGetServersPing() => throw new NotSupportedException();

            public Task StopGetServersPing() => throw new NotSupportedException();

            public Task CloseClientConnections(CancellationToken token) => throw new NotSupportedException();

            // TODO: Serverless refresh support.
            public Task<RefreshConnectionAuthResult> RefreshConnectionAuthAsync(RefreshAuthMessage message, HubServiceEndpoint preferredEndpoint = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task<GetConnectionClaimsResult> GetConnectionClaimsAsync(GetConnectionClaimsMessage message, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            #endregion Not supported method or properties
        }
    }
}
