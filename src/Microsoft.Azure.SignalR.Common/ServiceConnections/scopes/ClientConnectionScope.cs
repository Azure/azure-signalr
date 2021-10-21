// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Azure.SignalR.Common.Utilities;

namespace Microsoft.Azure.SignalR
{
    /// <summary>
    /// Represents a disposable scope able to carry connection properties along with the execution context flow
    /// </summary>
    internal class ClientConnectionScope : IDisposable
    {
        private readonly bool _needCleanup;

        internal ClientConnectionScope() : this(default, default, default)
        {
        }

        protected internal ClientConnectionScope(HubServiceEndpoint endpoint, IServiceConnection outboundConnection, bool isDiagnosticClient)
        {
            // Only allow to carry one copy of connection properties regardless of how many nested scopes are created
            if (!IsScopeEstablished)
            {
                _needCleanup = true;

                // The lifetime of the async local we're about to create can be much longer than some of the objects we store inside.
                // Instances of IServiceConnection can be stopped and replaced over time and nobody should keep references to the old ones.
                // So we keep them inside async local wrapped in weak references to avoid unnecessarily prolonging their lifetime.
                ConcurrentDictionary<long, WeakReference<IServiceConnection>> dict = new ConcurrentDictionary<long, WeakReference<IServiceConnection>>();
                if (endpoint != null)
                {
                    dict.TryAdd(endpoint.UniqueIndex, new WeakReference<IServiceConnection>(outboundConnection));
                }

                ScopePropertiesAccessor<ClientConnectionScopeProperties>.Current =
                    new ScopePropertiesAccessor<ClientConnectionScopeProperties>()
                    {
                        Properties = new ClientConnectionScopeProperties()
                        {
                            OutboundServiceConnections = dict,
                            IsDiagnosticClient = isDiagnosticClient
                        }
                    };
            }
            else
            {
                Debug.Assert(outboundConnection == default && isDiagnosticClient == default, "Attempt to replace an already established scope");
            }
        }

        public void Dispose()
        {
            if (_needCleanup)
            {
                // shallow cleanup since we don't want any running tasks or threads
                // (whose execution contexts can still carry a copy of async local)
                // to suddenly change behavior once we're done with disposing
                ScopePropertiesAccessor<ClientConnectionScopeProperties>.Current = null;
            }
        }

        internal static bool IsScopeEstablished => ScopePropertiesAccessor<ClientConnectionScopeProperties>.Current != null;

        internal static ConcurrentDictionary<long, WeakReference<IServiceConnection>> OutboundServiceConnections
        {
            get => ScopePropertiesAccessor<ClientConnectionScopeProperties>.Current?.Properties?.OutboundServiceConnections;
            set 
            {
                var currentProps = ScopePropertiesAccessor<ClientConnectionScopeProperties>.Current?.Properties;
                if (currentProps != null)
                {
                    currentProps.OutboundServiceConnections = value;
                }
            }
        }

        internal static bool IsDiagnosticClient
        {
            get => ScopePropertiesAccessor<ClientConnectionScopeProperties>.Current?.Properties?.IsDiagnosticClient ?? false;
            set
            {
                var currentProps = ScopePropertiesAccessor<ClientConnectionScopeProperties>.Current?.Properties;
                if (currentProps != null)
                {
                    currentProps.IsDiagnosticClient = value;
                }
            }
        }

        private class ClientConnectionScopeProperties
        {
            public ConcurrentDictionary<long, WeakReference<IServiceConnection>> OutboundServiceConnections { get; set; }

            public bool IsDiagnosticClient { get; set; }
        }
    }
}
