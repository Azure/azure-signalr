// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Serialization;
using Microsoft.Azure.SignalR.Common;

namespace Microsoft.Azure.SignalR.Management
{
    [Serializable]
    public class AzureSignalRUnauthorizationException : AzureSignalRException
    {
        public const string _message = "Authorization failed. Make sure you provide the correct connection string and have the access to the resource.";

        public AzureSignalRUnauthorizationException(Exception ex, string requestUri) : base(String.IsNullOrEmpty(requestUri) ? _message : $"{_message} Request Uri: {requestUri}", ex)
        {
        }

        public AzureSignalRUnauthorizationException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}