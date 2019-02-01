// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.Azure.SignalR.Common
{
    [Serializable]
    public class AzureSignalRUnauthorizedException : AzureSignalRException
    {
        private const string ErrorMessage = "Authorization failed. Make sure you provide the correct connection string and have access to the resource.";

        public AzureSignalRUnauthorizedException(string requestUri, Exception innerException) : base(string.IsNullOrEmpty(requestUri) ? ErrorMessage : $"{ErrorMessage} Request Uri: {requestUri}", innerException)
        {
        }

        protected AzureSignalRUnauthorizedException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}