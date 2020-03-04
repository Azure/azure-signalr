// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    internal class HubServiceEndpoint : ServiceEndpoint
    {
        private readonly TaskCompletionSource<bool> _scaleTcs;

        public HubServiceEndpoint(
            string hub, 
            IServiceEndpointProvider provider, 
            ServiceEndpoint endpoint, 
            bool needScaleTcs = false
            ) : base(endpoint)
        {
            Hub = hub;
            Provider = provider;
            _scaleTcs = needScaleTcs ? new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously) : null;
        }

        internal HubServiceEndpoint() : base() { }

        public string Hub { get; }

        public IServiceEndpointProvider Provider { get; }

        public IServiceConnectionContainer ConnectionContainer { get; set; }

        /// <summary>
        /// Task waiting for HubServiceEndpoint turn ready when live add/remove endpoint
        /// </summary>
        public Task ScaleTask => _scaleTcs?.Task ?? Task.CompletedTask;

        public void CompleteScale()
        {
            _scaleTcs?.TrySetResult(true);
        }
    }
}
