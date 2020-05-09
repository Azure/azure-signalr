// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR.Common
{
    /// <summary>
    /// Represents a generic nestable scope for grouping operations with the same service communication policy.
    /// Derived classes must ensure that the service protocol can understand and transmit this policy to the service.
    /// </summary>
    /// The following is ensured for all send operations to the service within the scope:
    /// -  the same service connection is used in ServiceTransportType.Persistent mode
    /// -  the same service connection is used for calls outside of hub call context
    /// -  only the value of the most nested scope will flow along with the messages
    /// 
    /// <typeparam name="TNestableProps">Type representing policy to flow</typeparam>
    public class ServiceCommunicationScope<TNestableProps> : IDisposable
    {
        private ScopePropertiesAccessor<TNestableProps> _previousScope;
        private ClientConnectionScopeInternal _serviceConnectionScope;

        protected ServiceCommunicationScope(TNestableProps properties)
        {
            _previousScope = ScopePropertiesAccessor<TNestableProps>.Current;
            ScopePropertiesAccessor<TNestableProps>.Current = new ScopePropertiesAccessor<TNestableProps>() { Properties = properties };
            _serviceConnectionScope = new ClientConnectionScopeInternal();
        }

        /// <summary>
        /// provides access to current scope properties 
        /// </summary>
        public static TNestableProps CurrentScopeProperties => ScopePropertiesAccessor<TNestableProps>.Current.Properties;

        /// <summary>
        /// Performs 'deep' cleanup of the current scope context and restores the previous one
        /// </summary>
        public void Dispose()
        {
            _serviceConnectionScope.Dispose();

            // Cleanup references to the properties within the current scope before it gets replaced with the previous one
            // This ensures that all unawaited tasks created within this scope will not leak references to TNestableProps
            ScopePropertiesAccessor<TNestableProps>.Current.Properties = default;
            ScopePropertiesAccessor<TNestableProps>.Current = _previousScope;
        }
    }
}
