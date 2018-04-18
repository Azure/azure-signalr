// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    public class CloudSignalR
    {
        private IHubMessageSender _hubMessageSender;
        private IConnectionServiceProvider _connectionServiceProvider;

        public CloudSignalR(IHubMessageSender hubMessageSender,
            IConnectionServiceProvider connectionServiceProvider)
        {
            _hubMessageSender = hubMessageSender;
            _connectionServiceProvider = connectionServiceProvider;
        }

        public SignalRServiceContext<THub> CreateServiceContext<THub>() where THub : Hub
        {
            var signalrServiceHubContext = new SignalRServiceHubContext<THub>(_connectionServiceProvider, _hubMessageSender);
            return new SignalRServiceContext<THub>(signalrServiceHubContext);
        }
    }
}
