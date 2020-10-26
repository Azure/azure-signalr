// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.Azure.SignalR.Common;

namespace Microsoft.Azure.SignalR.Emulator.HubEmulator
{
    internal sealed partial class HttpUpstreamPropertiesFeature : IHttpUpstreamPropertiesFeature
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
            UserIdentifier = connectionContext.GetUserIdentifier();
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
    }
}
