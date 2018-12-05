using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.ServerlessAgent
{
    public class UserGroupsManager : IUserGroupManager
    {
        IHubLifetimeManagerExtension _lifetimeManager;
        public UserGroupsManager(IHubLifetimeManagerExtension lifetimeManager)
        {
            _lifetimeManager = lifetimeManager;
        }

        public Task AddToGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default)
        {
            return _lifetimeManager.UserAddToGroupAsync(userId, groupName, cancellationToken);
        }

        public Task RemoveFromGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default)
        {
            return _lifetimeManager.UserRemoveFromGroupAsync(userId, groupName, cancellationToken);
        }
    }
}