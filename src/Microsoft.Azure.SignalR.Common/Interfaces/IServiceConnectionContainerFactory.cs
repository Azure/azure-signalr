﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    interface IServiceConnectionContainerFactory
    {
        IServiceConnectionContainer Create(string hub);
    }
}
