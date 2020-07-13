// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal static class IMessageWithTracingIdExtensions
    {
        public static ExtensibleServiceMessage WithTracingId<T>(this T message) where T : ExtensibleServiceMessage, IMessageWithTracingId
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