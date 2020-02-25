// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    internal class HubServiceEndpoint : ServiceEndpoint
    {
        public HubServiceEndpoint(string hub, IServiceEndpointProvider provider, ServiceEndpoint endpoint) : base(endpoint)
        {
            Hub = hub;
            Provider = provider;
        }

        internal HubServiceEndpoint() : base() { }

        public string Hub { get; }

        public IServiceEndpointProvider Provider { get; }

        /// <summary>
        /// Task waiting for HubServiceEndpoint turn ready to add negotiation, use when live add ServiceEndpoint
        /// </summary>
        public TaskCompletionSource<bool> Ready { get; set; }

        /// <summary>
        /// Task waiting for HubServiceEndpoint turn offline without clients, use when live remove ServiceEndpoint
        /// </summary>
        public TaskCompletionSource<bool> Offline { get; set; }
    }
}
