// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net.WebSockets;

namespace Microsoft.Azure.SignalR.Common
{
    internal static class ExceptionExtensions
    {
        internal static Exception WrapAsAzureSignalRException(this Exception e)
        {
            switch (e)
            {
                case WebSocketException webSocketException:
                    if (e.Message.StartsWith("The server returned status code \"401\""))
                    {
                        return new AzureSignalRUnauthorizedException(webSocketException);
                    }
                    return e;
                default: return e;
            }
        }
    }
}