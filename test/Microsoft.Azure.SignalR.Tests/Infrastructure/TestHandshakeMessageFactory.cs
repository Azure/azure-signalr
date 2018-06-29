// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.Tests.Infrastructure
{
    class TestHandshakeMessageFactory : IHandshakeMessageFactory
    {
        private readonly string _errorMessage;

        public TestHandshakeMessageFactory(string errorMessage = null)
        {
            _errorMessage = errorMessage ?? string.Empty;
        }

        public ServiceMessage GetHandshakeResponseMessage()
        {
            return new HandshakeResponseMessage(_errorMessage);
        }
    }
}
