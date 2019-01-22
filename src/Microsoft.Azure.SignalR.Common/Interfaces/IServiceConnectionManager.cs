﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.SignalR
{
    interface IServiceConnectionManager
    {
        IServiceConnection CreateServiceConnection();

        void DisposeServiceConnection(IServiceConnection connection);
    }
}
