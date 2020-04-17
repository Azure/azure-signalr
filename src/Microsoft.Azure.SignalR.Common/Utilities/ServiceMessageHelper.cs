// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal static class ServiceMessageHelper
    {
        // todo: generate a shorter ID
        public static string GenerateMessageId()
        {
            return Guid.NewGuid().ToString();
        }

        public static string GetMessageId(ServiceMessage serviceMessage)
        {
            if (serviceMessage is MulticastDataMessage multicastDataMessage)
            {
                return multicastDataMessage.MessageId;
            }

            return null;
        }
    }
}
