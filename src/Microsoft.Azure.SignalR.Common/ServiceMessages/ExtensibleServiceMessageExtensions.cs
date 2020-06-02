// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.SignalR.Common.ServiceConnections;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal static class ExtensibleServiceMessageExtensions
    {
        public static ExtensibleServiceMessage WithMessageId(this ExtensibleServiceMessage message)
        {
            var id = MessageIdHelper.Generate(ClientConnectionScope.IsDiagnosticClient);
            if (message is IMessageWithTracingId messageWithId)
            {
                messageWithId.TracingId = id;
            }
            return message;
        }
    }
}
