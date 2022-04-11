// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    internal class HubServiceEndpoint : ServiceEndpoint
    {
        private readonly TaskCompletionSource<bool> _scaleTcs;
        private readonly ServiceEndpoint _endpoint;
        private readonly long _uniqueIndex;
        private static long s_currentIndex;

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
            _uniqueIndex = Interlocked.Increment(ref s_currentIndex);
        }

        public string Hub { get; }

        public override string Name => _endpoint.Name;
        public override Uri ClientEndpoint => _endpoint.ClientEndpoint;

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

        public long UniqueIndex => _uniqueIndex;
    }
}
