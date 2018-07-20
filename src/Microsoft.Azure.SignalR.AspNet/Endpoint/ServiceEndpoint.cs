// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal class ServiceEndpoint : IServiceEndpoint
    {
        public ServiceEndpoint(ServiceOptions options)
        {
        }

        public string Endpoint => throw new NotImplementedException();

        public string AccessToken => throw new NotImplementedException();

        public string GenerateClientAccessToken(IEnumerable<Claim> claims = null, TimeSpan? lifetime = null)
        {
            throw new NotImplementedException();
        }

        public string GenerateServerAccessToken(string hubName, string userId, TimeSpan? lifetime = null)
        {
            throw new NotImplementedException();
        }

        public string GetClientEndpoint()
        {
            throw new NotImplementedException();
        }

        public string GetServerEndpoint(string hubName)
        {
            throw new NotImplementedException();
        }
    }
}
