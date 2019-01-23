// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Runtime.Serialization;
using Microsoft.Azure.SignalR.Common;

namespace Microsoft.Azure.SignalR.Management
{
    [Serializable]
    public class AzureSignalRRuntimeException : AzureSignalRException
    {
        public AzureSignalRRuntimeException(Exception ex, string requestUri) : base($"SignalR service runtime error. Request Uri: {requestUri}")
        {
        }

        public AzureSignalRRuntimeException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}