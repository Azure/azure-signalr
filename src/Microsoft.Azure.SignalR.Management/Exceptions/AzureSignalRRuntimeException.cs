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
        public const string _message = "Azure SignalR service runtime error.";

        public AzureSignalRRuntimeException(string requestUri, Exception innerException) : base(String.IsNullOrEmpty(requestUri) ? _message : $"{_message} Request Uri: {requestUri}", innerException)
        {
        }

        protected AzureSignalRRuntimeException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}