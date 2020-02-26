// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    internal class HubServiceEndpoint : ServiceEndpoint
    {
        public HubServiceEndpoint(
            string hub, 
            IServiceEndpointProvider provider, 
            ServiceEndpoint endpoint, 
            TaskCompletionSource<bool> scaleTcs = null
            ) : base(endpoint)
        {
            Hub = hub;
            Provider = provider;
            ScaleTcs = scaleTcs;
        }

        internal HubServiceEndpoint() : base() { }

        public string Hub { get; }

        public IServiceEndpointProvider Provider { get; }

        /// <summary>
        /// Task waiting for HubServiceEndpoint turn ready when live add/remove endpoint
        /// </summary>
        public TaskCompletionSource<bool> ScaleTcs { get; }
    }
}
