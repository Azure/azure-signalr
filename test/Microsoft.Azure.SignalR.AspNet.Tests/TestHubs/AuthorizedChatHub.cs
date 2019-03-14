using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;

namespace Microsoft.Azure.SignalR.AspNet.Tests.TestHubs
{
    [Authorize, HubName("authchat")]
    public class AuthorizedChatHub : Hub
    {
    }
}
