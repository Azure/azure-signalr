using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.Tests.Infrastructure
{
    class TestHandshackMessageFactory : IHandshackMessageFactory
    {
        private readonly string _errorMessage;

        public TestHandshackMessageFactory(string errorMessage = null)
        {
            _errorMessage = errorMessage ?? string.Empty;
        }

        public ServiceMessage GetHandshackResposeMessage()
        {
            return new HandshakeResponseMessage(_errorMessage);
        }
    }
}
