// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Management
{
    public class ManagementHubOptionsSetup : IConfigureOptions<HubOptions>
    {
        internal static TimeSpan DefaultHandshakeTimeout => TimeSpan.FromSeconds(15);

        internal static TimeSpan DefaultKeepAliveInterval => TimeSpan.FromSeconds(15);

        internal const int DefaultMaximumMessageSize = 32 * 1024;

        internal const int DefaultStreamBufferCapacity = 10;

        private readonly List<string> _defaultProtocols = new List<string>();

        public ManagementHubOptionsSetup(IEnumerable<IHubProtocol> protocols)
        {
            foreach (var hubProtocol in protocols)
            {
                _defaultProtocols.Add(hubProtocol.Name);
            }
        }

        public void Configure(HubOptions options)
        {
            if (options.KeepAliveInterval == null)
            {
                // The default keep - alive interval. This is set to exactly half of the default client timeout window,
                // to ensure a ping can arrive in time to satisfy the client timeout.
                options.KeepAliveInterval = DefaultKeepAliveInterval;
            }

            if (options.HandshakeTimeout == null)
            {
                options.HandshakeTimeout = DefaultHandshakeTimeout;
            }
#if NETCOREAPP3_0
            if (options.MaximumReceiveMessageSize == null)
            {
                options.MaximumReceiveMessageSize = DefaultMaximumMessageSize;
            }

            if (options.StreamBufferCapacity == null)
            {
                options.StreamBufferCapacity = DefaultStreamBufferCapacity;
            }
#endif
            if (options.SupportedProtocols == null)
            {
                options.SupportedProtocols = new List<string>();
            }

            foreach (var protocol in _defaultProtocols)
            {
                options.SupportedProtocols.Add(protocol);
            }

        }
    }
}
