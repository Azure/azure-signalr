// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.SignalR.Common.Utilities;
using System;
using System.Diagnostics;

namespace Microsoft.Azure.SignalR.Common.ServiceConnections
{
    /// <summary>
    /// Represents a disposable scope able to carry connection properties along with the execution context flow
    /// </summary>
    internal class ClientConnectionScope : IDisposable
    {
        private bool _needCleanup;

        internal ClientConnectionScope() : this(default, default)
        {
        }

        protected internal ClientConnectionScope(IServiceConnection outboundConnection, bool isDiagnosticClient)
        {
            // Only allow to carry one copy of connection properties regardless of how many nested scopes are created
            if (!IsScopeEstablished)
            {
                _needCleanup = true;
                ScopePropertiesAccessor<ClientConnectionScopeProperties>.Current =
                            new ScopePropertiesAccessor<ClientConnectionScopeProperties>()
                            {
                                Properties = new ClientConnectionScopeProperties()
                                {
                                    OutboundServiceConnection = outboundConnection,
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
                // shallow cleanup since we don't want any execution contexts in unawaited tasks 
                // to suddenly change behavior once we're done with disposing
                ScopePropertiesAccessor<ClientConnectionScopeProperties>.Current = null;
            }
        }

        internal static bool IsScopeEstablished => ScopePropertiesAccessor<ClientConnectionScopeProperties>.Current != null;

        internal static IServiceConnection OutboundServiceConnection
        {
            get => ScopePropertiesAccessor<ClientConnectionScopeProperties>.Current?.Properties?.OutboundServiceConnection;
            set 
            {
                var currentProps = ScopePropertiesAccessor<ClientConnectionScopeProperties>.Current?.Properties;
                if (currentProps != null)
                {
                    currentProps.OutboundServiceConnection = value;
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

        // todo: extend with client connection tracking/logging accessors
        private class ClientConnectionScopeProperties
        {
            public IServiceConnection OutboundServiceConnection { get; set; }

            public bool IsDiagnosticClient { get; set; }
        }
    }
}
