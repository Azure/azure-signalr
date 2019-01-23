// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Serialization;
using Microsoft.Azure.SignalR.Common;

namespace Microsoft.Azure.SignalR.Management
{
    [Serializable]
    public class AzureSignalRIncorrectEndpointException : AzureSignalRException
    {
        public AzureSignalRIncorrectEndpointException(Exception ex, string requestUri) : base($"Endpoint incorrect or DNS error. Request Uri: {requestUri}")
        {
        }

        public AzureSignalRIncorrectEndpointException(SerializationInfo info, StreamingContext context): base(info, context)
        {
        }
    }
}