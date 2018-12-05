using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.ServerlessAgent
{
    public interface IHubLifetimeManagerExtension
    {
        Task UserAddToGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default);
        Task UserRemoveFromGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default);

    }
}
