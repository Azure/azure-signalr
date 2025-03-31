// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace Microsoft.Azure.SignalR.Internals;

internal sealed class SignalRServerActivitySource
{
    public static readonly SignalRServerActivitySource Instance = new();

    public ActivitySource ActivitySource { get; } = new ActivitySource("Microsoft.Azure.SignalR.Server");
}
