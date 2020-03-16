// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    internal class HubServiceEndpoint : ServiceEndpoint
    {
        private readonly TaskCompletionSource<bool> _scaleTcs;
        private readonly ServiceEndpoint _endpoint;

        public HubServiceEndpoint(
            string hub, 
            IServiceEndpointProvider provider, 
            ServiceEndpoint endpoint, 
            bool needScaleTcs = false
            ) : base(endpoint)
        {
            Hub = hub;
            Provider = provider;
            _endpoint = endpoint;
            _scaleTcs = needScaleTcs ? new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously) : null;
        }

        // for tests
        internal HubServiceEndpoint() : base() 
        {
            _endpoint = new ServiceEndpoint();
        }

        public string Hub { get; }

        public override string Name => _endpoint.Name;

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
