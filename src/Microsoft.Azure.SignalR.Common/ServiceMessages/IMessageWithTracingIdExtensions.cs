// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal static class IMessageWithTracingIdExtensions
    {
        public static T WithTracingId<T>(this T message) where T : IMessageWithTracingId
        {
            if (ServiceConnectionContainerScope.EnableMessageLog || ClientConnectionScope.IsDiagnosticClient)
            {
                var id = MessageWithTracingIdHelper.Generate(ClientConnectionScope.IsDiagnosticClient);
                message.TracingId = id;
                return message;
            }
            return message;
        }
    }
}