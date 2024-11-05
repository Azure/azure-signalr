// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR.AspNet;

internal static class AspNetConstants
{
    public static class QueryString
    {
        public const string ConnectionToken = "connectionToken";
    }

    public static class Context
    {
        public const string AzureSignalRTransportKey = "signalr.transport";
    }
}
