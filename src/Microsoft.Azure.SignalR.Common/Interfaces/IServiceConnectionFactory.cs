﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    interface IServiceConnectionFactory
    {
        IServiceConnection Create(IConnectionFactory connectionFactory, IServiceMessageHandler serviceMessageHandler, ServerConnectionType type);
    }
}
