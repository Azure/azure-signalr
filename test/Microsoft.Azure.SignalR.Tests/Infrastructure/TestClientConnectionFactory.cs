﻿using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.Tests
{
    class TestClientConnectionFactory : IClientConnectionFactory
    {
        public IList<ClientConnectionContext> Connections = new List<ClientConnectionContext>();

        public ClientConnectionContext CreateConnection(OpenConnectionMessage message, Action<HttpContext> configureContext = null)
        {
            var context = new ClientConnectionContext(message, configureContext);
            Connections.Add(context);
            return context;
        }
    }
}
