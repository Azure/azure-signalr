using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.SignalR.ServerlessAgent
{
    public abstract class HubLifetimeManagerBase<THub> : HubLifetimeManager<THub> where THub : Hub
    {
        protected AgentContext _context;
        protected RestApis _apis;
    }
}
