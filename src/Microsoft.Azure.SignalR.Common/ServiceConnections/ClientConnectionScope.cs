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

        internal ClientConnectionScope() : this(default)
        {
        }

        protected internal ClientConnectionScope(ClientConnectionScopeProperties properties)
        {
            // Only allow to carry one copy of connection properties regardless of how many nested scopes are created
            if (ScopePropertiesAccessor<ClientConnectionScopeProperties>.Current == null)
            {
                _needCleanup = true;
                ScopePropertiesAccessor<ClientConnectionScopeProperties>.Current = new ScopePropertiesAccessor<ClientConnectionScopeProperties>() { Properties = properties };
            }
            else if (properties != null)
            { 
                Debug.Assert(false, "Attempt to replace an already established scope");
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

        internal static ScopePropertiesAccessor<ClientConnectionScopeProperties> CurrentScopeAccessor => ScopePropertiesAccessor<ClientConnectionScopeProperties>.Current;
    }
}
