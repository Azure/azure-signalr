// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.Azure.SignalR.Common
{
    [Serializable]
    public class AzureSignalRNoEndpointAvailableException : AzureSignalRException
    {
        public AzureSignalRNoEndpointAvailableException() : base("No Azure Service endpoint available.")
        {
        }

        protected AzureSignalRNoEndpointAvailableException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
