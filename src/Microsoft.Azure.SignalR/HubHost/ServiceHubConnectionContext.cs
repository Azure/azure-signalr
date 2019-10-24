// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Security.Claims;
using System.Threading;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Azure.SignalR
{
    /// <summary>
    /// ServiceHubConnectionContext is only passed to IUserIdProvider.GetUserId as a parameter.
    /// Only HttpContext and User properties are available for access.
    /// Exception will be thrown when other properties are accessed.
    /// </summary>
    internal class ServiceHubConnectionContext : HubConnectionContext
    {
        public const string UnavailableErrorTemplate = " property is not available in context of Azure SignalR Service.";
        public const string ConnectionAbortedUnavailableError = nameof(ConnectionAborted) + UnavailableErrorTemplate;
        public const string ConnectionIdUnavailableError = nameof(ConnectionId) + UnavailableErrorTemplate;
        public const string ItemsUnavailableError = nameof(Items) + UnavailableErrorTemplate;
        public const string ProtocolUnavailableError = nameof(Protocol) + UnavailableErrorTemplate;

#if !NETSTANDARD2_0
        public ServiceHubConnectionContext(HttpContext context)
            : base(new DummyConnectionContext(context), 
                  new HubConnectionContextOptions() { KeepAliveInterval = TimeSpan.MaxValue }, 
                  NullLoggerFactory.Instance)
        {
        }
#else
        public ServiceHubConnectionContext(HttpContext context)
            : base(new DummyConnectionContext(context), TimeSpan.MaxValue, NullLoggerFactory.Instance)
        {
        }
#endif

        public override CancellationToken ConnectionAborted => throw new InvalidOperationException(ConnectionAbortedUnavailableError);

        public override string ConnectionId => throw new InvalidOperationException(ConnectionIdUnavailableError);

        public override IDictionary<object, object> Items => throw new InvalidOperationException(ItemsUnavailableError);

        public override IHubProtocol Protocol => throw new InvalidOperationException(ProtocolUnavailableError);

        private class DummyConnectionContext : ConnectionContext,
                                               IHttpContextFeature,
                                               IConnectionUserFeature
        {
            public DummyConnectionContext(HttpContext context)
            {
                HttpContext = context;
                User = context?.User;

                Features = new FeatureCollection();
                Features.Set<IHttpContextFeature>(this);
                Features.Set<IConnectionUserFeature>(this);
            }

            public override string ConnectionId { get; set; }

            public override IFeatureCollection Features { get; }

            public override IDictionary<object, object> Items { get; set; }

            public override IDuplexPipe Transport { get; set; }

            public HttpContext HttpContext { get; set; }

            public ClaimsPrincipal User { get; set; }
        }
    }
}
