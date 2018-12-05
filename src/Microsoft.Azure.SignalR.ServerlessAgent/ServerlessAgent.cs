using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.SignalR.ServerlessAgent
{
    public class ServerlessAgent : IHubContext<Hub>
    {
        private IHubContext<Hub> _hubContext;

        public ServerlessAgent(IHubContext<Hub> hubContext, IHubLifetimeManagerExtension lifetimeManager)
        {
            _hubContext = hubContext;
            UserGroups = new UserGroupsManager(lifetimeManager); 
        }

        public IHubClients Clients => _hubContext.Clients;

        public IGroupManager Groups => _hubContext.Groups;

        public IUserGroupManager UserGroups { get; }
    }
}
