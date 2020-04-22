// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.Azure.SignalR
{
    internal class ClientConnectionScopeProperties
    {
        public IServiceConnection ServiceConnection { get; set; }

        // todo: add additional properties to flow with the scope here
    }

    internal class ServiceConnectionScopeHolder
    {
        private static AsyncLocal<ServiceConnectionScopeHolder> s_AsyncLocalSCC = new AsyncLocal<ServiceConnectionScopeHolder>();
        internal static ServiceConnectionScopeHolder Current
        {
            get => s_AsyncLocalSCC.Value;
            set => s_AsyncLocalSCC.Value = value;
        }

        public ClientConnectionScopeProperties Properties { get; set; }
    }

    internal class ServiceConnectionScopeInternal : IDisposable
    {
        readonly private bool _needCleanup = false;

        public ServiceConnectionScopeInternal() : this(default)
        {
        }

        internal ServiceConnectionScopeInternal(ClientConnectionScopeProperties properties)
        {
            if (ServiceConnectionScopeHolder.Current == null)
            {
                ServiceConnectionScopeHolder.Current = new ServiceConnectionScopeHolder() { Properties = properties };
                _needCleanup = true;
            }
            else
            {
                Debug.Assert(properties == null,
                    $"Attempt to replace existing connection scope  {ServiceConnectionScopeHolder.Current.Properties.ServiceConnection?.GetHashCode()} with new connection: {properties?.ServiceConnection?.GetHashCode()}");
            }
        }

        internal static ServiceConnectionScopeHolder Holder => ServiceConnectionScopeHolder.Current;

        public void Dispose()
        {
            {
                if (_needCleanup)
                {
                    ServiceConnectionScopeHolder.Current = null;
                }
            }
        }
    }
}
