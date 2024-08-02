using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.Tests;

internal class TestClientConnectionFactory : IClientConnectionFactory
{
    public IList<ClientConnectionContext> Connections = new List<ClientConnectionContext>();

    public IClientConnection CreateConnection(OpenConnectionMessage message, Action<HttpContext> configureContext = null)
    {
        var context = new ClientConnectionContext(message, configureContext, closeTimeOutMilliseconds: 10000);
        Connections.Add(context);
        return context;
    }
}
