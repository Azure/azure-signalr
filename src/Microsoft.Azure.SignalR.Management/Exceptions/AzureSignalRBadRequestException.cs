// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Serialization;
using Microsoft.Azure.SignalR.Common;

namespace Microsoft.Azure.SignalR.Management
{
    [Serializable]
    public class AzureSignalRBadRequestException : AzureSignalRException
    {
        private const string _message = "Bad Request. Caused by one or more of the following reasons: Invalid hub name, invalid group name, null or empty method name, and invalid message.";

        public AzureSignalRBadRequestException(Exception ex, string requestUri) : base(String.IsNullOrEmpty(requestUri) ? _message : $"{_message} Request Uri: {requestUri}", ex)
        {
        }

        public AzureSignalRBadRequestException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}