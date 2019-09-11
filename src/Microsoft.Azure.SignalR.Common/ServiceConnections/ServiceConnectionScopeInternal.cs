// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceConnectionScopeHolder
    {
        private static AsyncLocal<ServiceConnectionScopeHolder> s_AsyncLocalSCC = new AsyncLocal<ServiceConnectionScopeHolder>();
        internal static ServiceConnectionScopeHolder Current
        {
            get => s_AsyncLocalSCC.Value;
            set => s_AsyncLocalSCC.Value = value;
        }

        public IServiceConnection ServiceConnection { get; set; }
        public bool ConnectionChanged { get; set; }
    }

    internal class ServiceConnectionScopeInternal : IDisposable 
    {
        readonly private bool _needCleanup = false;

        public ServiceConnectionScopeInternal() : this(default)
        {
        }

        internal ServiceConnectionScopeInternal(IServiceConnection connection)
        {
            if (ServiceConnectionScopeHolder.Current == null)
            {
                ServiceConnectionScopeHolder.Current = new ServiceConnectionScopeHolder() { ServiceConnection = connection };
                _needCleanup = true;
            }
            else
            {
                Debug.Assert(connection == null,
                    $"Attempt to replace existing connection scope  {ServiceConnectionScopeHolder.Current.ServiceConnection?.GetHashCode()} with new connection: {connection?.GetHashCode()}");
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
