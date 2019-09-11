// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR
{
    /// <summary>
    /// When using Azure SignalR Service ServiceConnectionScope class tries to ensure that all the messages being sent to clients 
    /// within its scope are using the same connection from App Server to the Service thus helping to ensure the order of messages.
    /// </summary>
    public class ServiceConnectionScope : IDisposable
    {
        ServiceConnectionScopeInternal _internalScope;

        /// <summary>
        /// All SignalR Hub method calls (and calls within the same async flow) already ensure that they will use the same connection to the service.
        /// So using this class makes difference only outside of Hub methods (e.g. when one got the reference to a Hub via DI or implemented custom queueing).
        ///
        /// Here is an example of using it from code outside of hub methods (pseudocode): 
        /// 
        ///     // ... this is a separate thread or a task not associated with any Hub method calls...
        ///     // all messages within ServiceConnectionScope will be sent out over the same connection to the serivce
        ///     using (var s = new ServiceConnectionScope())
        ///     {
        ///         while (moreDataToSend)
        ///         {
        ///             await hubInstance.Clients.Client(clientId).SendAsync("ClientMethod", GetData());
        ///         }
        ///     }
        /// </summary>
        public ServiceConnectionScope()
        {
            _internalScope = new ServiceConnectionScopeInternal();
        }

        /// <summary>
        /// Boolean flag indicating that the app server has sent the last message via a different service connection
        /// thus creating a possibility of out of order messages being delivered to the client(s).
        /// This propery could be useful both inside and outside of hub methods to detect such case and 
        /// to allow applications to invoke a (potentially much heavier) custom message order recovery mechanism.
        /// 
        /// Here is an example of using it inside a hub method (pseudocode): 
        /// class MyHub : Hub {
        ///    public async Task AHubMethod() {
        ///         using (var scope = new ServiceConnectionScope()) {
        ///             while (there_is_more_data_to_send) {
        ///                 try {
        ///                     await Clients.Client(targetId).SendAsync("ClientMethod", data);
        ///                 }
        ///                 catch () {...}
        ///                 finally {
        ///                     if (scope.ConnectionChanged) {
        ///                         InvokeACustomMechanismOfEnsuringRecentMessagesAreInOrder();
        ///                     }
        ///                 }
        ///             }
        ///         }
        ///     }
        /// }
        ///
        /// When used inside Hub methods this property will track connection changes between different calls to the Hub for the same client.
        /// TODO: need a good example of tracking that the connection has changed between calls: 
        /// recovery from connection change between AddClientToGroup and SendToGroup?
        /// </summary>
        public bool ConnectionChanged => ServiceConnectionScopeInternal.Holder == null ?
            false : ServiceConnectionScopeInternal.Holder.ConnectionChanged;

        public void Dispose()
        {
            _internalScope.Dispose();
        }
    }
}
