// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR.Tests;

internal static class ClientConnectionExtension
{
    public static ClientConnectionContext AsContext(this IClientConnection conn) => conn as ClientConnectionContext;
}
