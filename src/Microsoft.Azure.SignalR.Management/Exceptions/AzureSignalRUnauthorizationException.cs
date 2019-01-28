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
        public const string ErrorMessage = "Authorization failed. Make sure you provide the correct connection string and have the access to the resource.";

        public AzureSignalRUnauthorizationException(string requestUri, Exception innerException) : base(String.IsNullOrEmpty(requestUri) ? ErrorMessage : $"{ErrorMessage} Request Uri: {requestUri}", innerException)
        {
        }

        protected AzureSignalRUnauthorizationException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}