// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.Azure.SignalR.Common
{
    [Serializable]
    public class AzureSignalRNoPrimaryEndpointException : AzureSignalRException
    {
        public AzureSignalRNoPrimaryEndpointException() : base("No primary endpoint defined.")
        {
        }

#if NET8_0_OR_GREATER
        [Obsolete]
#endif
        protected AzureSignalRNoPrimaryEndpointException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
