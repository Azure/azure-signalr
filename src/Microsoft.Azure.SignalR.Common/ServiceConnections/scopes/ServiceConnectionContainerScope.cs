// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceConnectionContainerScope : IDisposable
    {
        private static readonly AsyncLocal<ServiceDiagnosticLogsContext> _asyncLocal = new AsyncLocal<ServiceDiagnosticLogsContext>();
        private readonly bool _needCleanup;
        public static bool IsScopeEstablished => _asyncLocal.Value != null;

        public static bool EnableMessageLog
        {
            get => _asyncLocal.Value?.EnableMessageLog ?? default;
        }

        public ServiceConnectionContainerScope(ServiceDiagnosticLogsContext props)
        {
            if (!IsScopeEstablished)
            {
                _needCleanup = true;
                _asyncLocal.Value = props;
            }
            else
            {
                Debug.Assert(!IsScopeEstablished, "Attempt to replace an already established scope");
            }
        }

        public void Dispose()
        {
            if (_needCleanup)
            {
                // shallow cleanup since we don't want any execution contexts in unawaited tasks
                // to suddenly change behavior once we're done with disposing
                _asyncLocal.Value = null;
            }
        }
    }
}
