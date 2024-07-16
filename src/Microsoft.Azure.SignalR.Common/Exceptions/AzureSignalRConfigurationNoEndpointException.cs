// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.Azure.SignalR.Common
{
    [Serializable]
    public class AzureSignalRConfigurationNoEndpointException : AzureSignalRException
    {
        public AzureSignalRConfigurationNoEndpointException() : base("No connection string was specified.")
        {
        }

#if NET8_0_OR_GREATER
        [Obsolete]
#endif
        protected AzureSignalRConfigurationNoEndpointException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
