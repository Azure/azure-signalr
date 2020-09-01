// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Common;

namespace Microsoft.Azure.SignalR.Emulator.HubEmulator
{
    public sealed class HttpUpstreamPropertiesFeature : IHttpUpstreamPropertiesFeature
    {
        private (IReadOnlyList<string> keys, IReadOnlyList<string> signatures) _cache;

        public string ConnectionId { get; set; }

        public string QueryString { get; }
        public IReadOnlyList<string> ClaimStrings { get; }

        public string Hub { get; }

        public string UserIdentifier { get; }

        public HttpUpstreamPropertiesFeature(ConnectionContext connectionContext, string hub)
        {
            var user = connectionContext.Features.Get<IConnectionUserFeature>()?.User;

            ClaimStrings = user?.Claims.Select(c => c.ToString()).ToList();
            UserIdentifier = GetUserIdentifier(connectionContext);
            ConnectionId = connectionContext.ConnectionId;
            var context = connectionContext.Features.Get<IHttpContextFeature>()?.HttpContext;

            Hub = hub;
            QueryString = context.Request?.QueryString.ToString();
        }

        public IReadOnlyList<string> GetSignatures(IReadOnlyList<string> keys)
        {
            var currentCache = _cache;
            if (currentCache.keys != keys)
            {
                currentCache = (keys, Utils.GetConnectionSignature(ConnectionId, keys).ToList());
                _cache = currentCache;
            }

            return currentCache.signatures;
        }

        internal static string GetUserIdentifier(ConnectionContext connectionContext)
        {
            var user = connectionContext.Features.Get<IConnectionUserFeature>()?.User;
            if (user != null)
            {
                var customUserIdClaim = user.FindFirst(Constants.ClaimTypes.UserIdClaimType);
                return customUserIdClaim != null
                    ? customUserIdClaim.Value
                    : user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            }

            return null;
        }
    }
}
