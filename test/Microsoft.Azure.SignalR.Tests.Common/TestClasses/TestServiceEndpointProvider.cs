using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.Tests.Common;

internal class TestServiceEndpointProvider : IServiceEndpointProvider
{
    public IWebProxy Proxy => throw new NotImplementedException();

    public Task<string> GenerateClientAccessTokenAsync(string hubName, IEnumerable<Claim> claims = null, TimeSpan? lifetime = null)
    {
        throw new NotImplementedException();
    }

    public IAccessTokenProvider GetServerAccessTokenProvider(string hubName, string serverId)
    {
        throw new NotImplementedException();
    }

    public string GetClientEndpoint(string hubName, string originalPath, string queryString)
    {
        throw new NotImplementedException();
    }

    public string GetServerEndpoint(string hubName)
    {
        throw new NotImplementedException();
    }
}