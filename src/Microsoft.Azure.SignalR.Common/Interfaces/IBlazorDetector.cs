// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR;

internal interface IBlazorDetector
{
    public bool IsBlazor(string hubName);

    public bool TrySetBlazor(string hubName, bool isBlazor);
}
