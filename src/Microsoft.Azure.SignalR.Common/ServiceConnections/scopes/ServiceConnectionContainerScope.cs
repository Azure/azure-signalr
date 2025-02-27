// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.Azure.SignalR;

internal class ServiceConnectionContainerScope : IDisposable
{
    private static readonly AsyncLocal<ServiceDiagnosticLogsContext> AsyncLocal = new();

    private readonly bool _needCleanup;

    public static bool IsScopeEstablished => AsyncLocal.Value != null;

    public static bool EnableMessageLog
    {
        get => AsyncLocal.Value?.EnableMessageLog ?? default;
    }

    public ServiceConnectionContainerScope(ServiceDiagnosticLogsContext props)
    {
        if (!IsScopeEstablished)
        {
            _needCleanup = true;
            AsyncLocal.Value = props;
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
            AsyncLocal.Value = null;
        }
    }
}
