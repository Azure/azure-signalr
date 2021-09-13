// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;

namespace Microsoft.Azure.SignalR.Common.Utilities
{
    internal class ScopePropertiesAccessor<TProps>
    {
        // Use async local with indirect reference to TProps to allow for deep cleanup
        private static readonly AsyncLocal<ScopePropertiesAccessor<TProps>> s_currentAccessor = new AsyncLocal<ScopePropertiesAccessor<TProps>>();

        protected internal static ScopePropertiesAccessor<TProps> Current
        {
            get => s_currentAccessor.Value;
            set => s_currentAccessor.Value = value;
        }

        internal TProps Properties { get; set; }
    }
}