using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.Tests.Infrastructure
{
    interface IHandshackMessageFactory
    {
        ServiceMessage GetHandshackResposeMessage();
    }
}
